﻿#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class DeployToUpgradeInfo : ITraitInfo, Requires<UpgradeManagerInfo>
	{
		[UpgradeGrantedReference]
		[Desc("The upgrades to grant while the actor is undeployed.")]
		public readonly string[] UndeployedUpgrades = { };

		[UpgradeGrantedReference, FieldLoader.Require]
		[Desc("The upgrades to grant after deploying and revoke before undeploying.")]
		public readonly string[] DeployedUpgrades = { };

		[Desc("The terrain types that this actor can deploy on to receive these upgrades. " +
			"Leave empty to allow any.")]
		public readonly HashSet<string> AllowedTerrainTypes = new HashSet<string>();

		[Desc("Can this actor deploy on slopes?")]
		public readonly bool CanDeployOnRamps = false;

		[Desc("Cursor to display when able to (un)deploy the actor.")]
		public readonly string DeployCursor = "deploy";

		[Desc("Cursor to display when unable to (un)deploy the actor.")]
		public readonly string DeployBlockedCursor = "deploy-blocked";

		[SequenceReference, Desc("Animation to play for deploying/undeploying.")]
		public readonly string DeployAnimation = null;

		[Desc("Facing that the actor must face before deploying. Set to -1 to deploy regardless of facing.")]
		public readonly int Facing = -1;

		[Desc("Sound to play when deploying.")]
		public readonly string DeploySound = null;

		[Desc("Sound to play when undeploying.")]
		public readonly string UndeploySound = null;

		public object Create(ActorInitializer init) { return new DeployToUpgrade(init.Self, this); }
	}

	public class DeployToUpgrade : IResolveOrder, IIssueOrder, INotifyCreated
	{
		enum DeployState { Undeployed, Deploying, Deployed }

		readonly Actor self;
		readonly DeployToUpgradeInfo info;
		readonly UpgradeManager manager;
		readonly bool checkTerrainType;
		readonly bool canTurn;
		readonly Lazy<ISpriteBody> body;

		DeployState deployState;

		public DeployToUpgrade(Actor self, DeployToUpgradeInfo info)
		{
			this.self = self;
			this.info = info;
			manager = self.Trait<UpgradeManager>();
			checkTerrainType = info.AllowedTerrainTypes.Count > 0;
			canTurn = self.Info.HasTraitInfo<IFacingInfo>();
			body = Exts.Lazy(self.TraitOrDefault<ISpriteBody>);
		}

		public void Created(Actor self)
		{
			OnUndeployCompleted();
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get { yield return new DeployOrderTargeter("DeployToUpgrade", 5,
				() => IsOnValidTerrain() ? info.DeployCursor : info.DeployBlockedCursor); }
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "DeployToUpgrade")
				return new Order(order.OrderID, self, queued);

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "DeployToUpgrade" || deployState == DeployState.Deploying)
				return;

			if (!order.Queued)
				self.CancelActivity();

			if (deployState == DeployState.Deployed)
			{
				self.QueueActivity(new CallFunc(Undeploy));
			}
			else if (deployState == DeployState.Undeployed)
			{
				// Turn to the required facing.
				if (info.Facing != -1 && canTurn)
					self.QueueActivity(new Turn(self, info.Facing));

				self.QueueActivity(new CallFunc(Deploy));
			}
		}

		bool IsOnValidTerrain()
		{
			return IsOnValidTerrainType() && IsOnValidRampType();
		}

		bool IsOnValidTerrainType()
		{
			if (!self.World.Map.Contains(self.Location))
				return false;

			if (!checkTerrainType)
				return true;

			var tileSet = self.World.TileSet;
			var tiles = self.World.Map.MapTiles.Value;
			var terrainType = tileSet[tileSet.GetTerrainIndex(tiles[self.Location])].Type;

			return info.AllowedTerrainTypes.Contains(terrainType);
		}

		bool IsOnValidRampType()
		{
			if (info.CanDeployOnRamps)
				return true;

			var ramp = 0;
			if (self.World.Map.Contains(self.Location))
			{
				var tile = self.World.Map.MapTiles.Value[self.Location];
				var ti = self.World.TileSet.GetTileInfo(tile);
				if (ti != null)
					ramp = ti.RampType;
			}

			return ramp == 0;
		}

		/// <summary>Play deploy sound and animation.</summary>
		void Deploy()
		{
			// Something went wrong, most likely due to deploy order spam and the fact that this is a delayed action.
			if (deployState != DeployState.Undeployed)
				return;

			if (!IsOnValidTerrain())
				return;

			if (!string.IsNullOrEmpty(info.DeploySound))
				Game.Sound.Play(info.DeploySound, self.CenterPosition);

			// Revoke upgrades that are used while undeployed.
			OnDeployStarted();

			// If there is no animation to play just grant the upgrades that are used while deployed.
			// Alternatively, play the deploy animation and then grant the upgrades.
			if (string.IsNullOrEmpty(info.DeployAnimation) || body.Value == null)
				OnDeployCompleted();
			else
				body.Value.PlayCustomAnimation(self, info.DeployAnimation, OnDeployCompleted);
		}

		/// <summary>Play undeploy sound and animation and after that revoke the upgrades.</summary>
		void Undeploy()
		{
			// Something went wrong, most likely due to deploy order spam and the fact that this is a delayed action.
			if (deployState != DeployState.Deployed)
				return;

			if (!string.IsNullOrEmpty(info.UndeploySound))
				Game.Sound.Play(info.UndeploySound, self.CenterPosition);

			OnUndeployStarted();

			// If there is no animation to play just grant the upgrades that are used while undeployed.
			// Alternatively, play the undeploy animation and then grant the upgrades.
			if (string.IsNullOrEmpty(info.DeployAnimation))
				OnUndeployCompleted();
			else
				body.Value.PlayCustomAnimationBackwards(self, info.DeployAnimation, OnUndeployCompleted);
		}

		void OnDeployStarted()
		{
			deployState = DeployState.Deploying;

			foreach (var up in info.UndeployedUpgrades)
				manager.RevokeUpgrade(self, up, this);
		}

		void OnDeployCompleted()
		{
			foreach (var up in info.DeployedUpgrades)
				manager.GrantUpgrade(self, up, this);

			deployState = DeployState.Deployed;
		}

		void OnUndeployStarted()
		{
			foreach (var up in info.DeployedUpgrades)
				manager.RevokeUpgrade(self, up, this);

			deployState = DeployState.Deploying;
		}

		void OnUndeployCompleted()
		{
			deployState = DeployState.Undeployed;

			foreach (var up in info.UndeployedUpgrades)
				manager.GrantUpgrade(self, up, this);
		}
	}
}
