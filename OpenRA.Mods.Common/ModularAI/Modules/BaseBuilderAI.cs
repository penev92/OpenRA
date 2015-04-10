// #region Copyright & License Information
// /*
//  * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
//  * This file is part of OpenRA, which is free software. It is made
//  * available to you under the terms of the GNU General Public License
//  * as published by the Free Software Foundation. For more information,
//  * see COPYING.
//  */
// #endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	[Desc("Logic module for ModularAI. Finds a random (non-intelligent) location to deploy idle base-builder actors.")]
	public class BaseBuilderAIInfo : ITraitInfo, IAILogicInfo, Requires<ModularAIInfo>
	{
		[Desc("Actor type names. Not deployed factories. Typically MCVs. Must have the `Transforms` trait.")]
		public readonly string[] BaseBuilderTypes = { "mcv" };

		[Desc("Actor type names. Typically Construction Yards.")]
		public readonly string[] MainBaseBuildingTypes = { "gacnst" };

		[Desc("Minimum number of cells to put between each base builder before attempting to deploy.")]
		public readonly int BaseExpansionRadius = 10;

		[Desc("Name of the AI personality this module belongs to.")]
		public readonly string AIName;

		[Desc("Names of build queues this module has access to.")]
		public string[] ProductionQueueNames;

		string IAILogicInfo.AIName { get { return AIName; } }

		public object Create(ActorInitializer init) { return new BaseBuilderAI(init.Self, this); }
	}

	public class BaseBuilderAI : IAILogic, INotifyIdle, INotifyAddedToWorld, INotifyBuildComplete
	{
		public Actor MainBaseBuilding { get; private set; }
		public readonly BaseBuilderAIInfo Info;

		readonly Actor self;
		readonly World world;
		readonly ModularAI ai;
		readonly int expansionRadius;
		readonly Dictionary<Actor, Transforms> baseBuilders;

		CPos? tryGetLatestConyardAtCell;
		public List<ProductionQueue> BuildQueues { get; private set; }

		public string AIName { get { return Info.AIName; } }

		public BaseBuilderAI(Actor self, BaseBuilderAIInfo info)
		{
			Info = info;
			this.self = self;
			world = self.World;
			BuildQueues = new List<ProductionQueue>();
			expansionRadius = info.BaseExpansionRadius;
			baseBuilders = new Dictionary<Actor, Transforms>();
			ai = self.TraitsImplementing<ModularAI>().FirstOrDefault(x => x.Info.Name == AIName);
		}

		public void TickIdle(Actor self)
		{
			if (!Info.BaseBuilderTypes.Contains(self.Info.Name))
				return;

			if (!baseBuilders.ContainsKey(self))
				baseBuilders.Add(self, self.Trait<Transforms>());

			TryToDeploy(self);
		}

		public void Tick(Actor self)
		{
			if (MainBaseBuilding == null || MainBaseBuilding.IsDead || !MainBaseBuilding.IsInWorld)
			{
				// Pick a new one
				MainBaseBuilding = ai.OwnedActors
					.FirstOrDefault(a => !a.IsDead && a.IsInWorld
						&& Info.MainBaseBuildingTypes.Contains(a.Info.Name));
			}
		}

		void TryPlaceBuilding(ProductionQueue productionQueue)
		{
			// TODO: Pick smarter locations

			var targetCell = world.Map.FindTilesInAnnulus(MainBaseBuilding.Location, 1, 6) // TODO: Sensible values
				.Where(world.Map.Contains).Random(world.SharedRandom);

			world.IssueOrder(new Order("PlaceBuilding", self, false)
			{
				TargetLocation = targetCell,
				TargetString = productionQueue.CurrentItem().Item,
				TargetActor = productionQueue.Actor,
				SuppressVisualFeedback = true
			});
		}

		void TryToDeploy(Actor baseBuilder)
		{
			var tryDeploy = new Order("DeployTransform", baseBuilder, true);
			var transforms = baseBuilders[baseBuilder];
			var deploysInto = transforms.IntoActor;

			if (MainBaseBuilding != null)
			{
				if (baseBuilder.IsMoving())
					return;

				var targetCell = PickDeploymentCell(baseBuilder.CenterPosition);

				ai.Debug("Try to deploy into {0} at {1}.", deploysInto, targetCell);

				var moveToDest = new Order("Move", baseBuilder, true)
				{
					TargetLocation = targetCell
				};

				world.AddFrameEndTask(w =>
				{
					w.IssueOrder(moveToDest);
					w.IssueOrder(tryDeploy);
				});

				return;
			}
			
			if (tryGetLatestConyardAtCell.HasValue)
			{
				var occupyingCell = world.ActorMap.GetUnitsAt(tryGetLatestConyardAtCell.Value);
				var atCell = occupyingCell.FirstOrDefault();

				if (occupyingCell.Count() > 1 ||
					atCell == null ||
					atCell.Owner != baseBuilder.Owner ||
					atCell.Info.Name != deploysInto)
				{
					tryGetLatestConyardAtCell = null;
					return;
				}

				MainBaseBuilding = atCell;
				return;
			}

			if (transforms.CanDeploy(baseBuilder.Location))
			{
				tryDeploy.TargetLocation = world.Map.CellContaining(baseBuilder.CenterPosition);
				world.IssueOrder(tryDeploy);
				tryGetLatestConyardAtCell = tryDeploy.TargetLocation;
			}
			else
			{
				var move = new Order("Move", baseBuilder, false)
				{
					TargetLocation = PickDeploymentCell(baseBuilder.CenterPosition)
				};

				world.IssueOrder(move);
			}
		}

		CPos PickDeploymentCell(WPos position)
		{
			var srcCell = world.Map.CellContaining(position);
			var targetCell = world.Map.FindTilesInAnnulus(
				srcCell,
				expansionRadius,
				expansionRadius + expansionRadius / 2) // TODO: Sensible value
				.Where(world.Map.Contains)
				.MinBy(c => (c - srcCell).LengthSquared);

			return targetCell;
		}

		public bool StartProduction(string actor)
		{
			var candidates = new List<ProductionQueue>();

			foreach (var productionQueue in BuildQueues)
				if (productionQueue.BuildableItems().Any(x => x.Name == actor))
					candidates.Add(productionQueue);

			if (!candidates.Any())
				return false;

			var queue = candidates.MinBy(x => x.CurrentRemainingTime);

			ai.Debug("Starting production of {0}.", actor);
			world.IssueOrder(Order.StartProduction(queue.Actor, actor, 1));

			return true;
		}

		public void AddedToWorld(Actor self)
		{
			var queues = self.TraitsImplementing<ProductionQueue>().Where(x => Info.ProductionQueueNames.Contains(x.Info.Type));
			BuildQueues.AddRange(queues);
		}

		public void BuildingComplete(Actor self)
		{
			if (MainBaseBuilding == null || MainBaseBuilding.IsDead || !MainBaseBuilding.IsInWorld)
				return;

			// The notification might not be for one of the queues this manages, but check just in case
			foreach (var productionQueue in BuildQueues.Where(q => q.CurrentDone))
				TryPlaceBuilding(productionQueue);
		}
	}
}