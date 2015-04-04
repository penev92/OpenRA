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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	[Desc("Logic module for ModularAI. Finds a random (non-intelligent) location to deploy idle base-builder actors.")]
	public class BaseBuilderAIInfo : ITraitInfo, Requires<ModularAIInfo>
	{
		[Desc("Actor type names. Not deployed factories. Typically MCVs. Must have the `Transforms` trait.")]
		public readonly string[] BaseBuilderTypes = { "mcv" };

		[Desc("Minimum number of cells to put between each base builder before attempting to deploy.")]
		public readonly int BaseExpansionRadius = 10;

		public object Create(ActorInitializer init) { return new BaseBuilderAI(init.Self, this); }
	}

	public class BaseBuilderAI : IAILogic, INotifyIdle
	{
		readonly ModularAI ai;
		readonly World world;
		readonly BaseBuilderAIInfo info;

		Actor mainBaseBuilding { get { return ai.MainBaseBuilding; } }
		readonly int expansionRadius;
		CPos? tryGetLatestConyardAtCell;

		public BaseBuilderAI(Actor self, BaseBuilderAIInfo info)
		{
			ai = self.Trait<ModularAI>();
			world = self.World;
			this.info = info;
			expansionRadius = info.BaseExpansionRadius;
			ai.RegisterModule(this);
		}

		public void TickIdle(Actor self)
		{
			if (!info.BaseBuilderTypes.Contains(self.Info.Name))
				return;

			TryToDeploy(self);
		}

		public void Tick(Actor self)
		{

		}

		void TryToDeploy(Actor baseBuilder)
		{
			var tryDeploy = new Order("DeployTransform", baseBuilder, true);
			var deploysInto = baseBuilder.Info.Traits.Get<TransformsInfo>().IntoActor;

			if (mainBaseBuilding != null)
			{
				if (baseBuilder.IsMoving())
					return;

				var srcCell = world.Map.CellContaining(baseBuilder.CenterPosition);
				var targetCell = world.Map.FindTilesInAnnulus(
					srcCell,
					expansionRadius,
					expansionRadius + expansionRadius / 2) // TODO: Sensible value
					.Where(world.Map.Contains)
					.MinBy(c => (c - srcCell).LengthSquared);

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
			else if (tryGetLatestConyardAtCell.HasValue)
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

				ai.SetMainBase(atCell);
				return;
			}

			tryDeploy.TargetLocation = world.Map.CellContaining(baseBuilder.CenterPosition);
			world.IssueOrder(tryDeploy);
			tryGetLatestConyardAtCell = tryDeploy.TargetLocation;
		}
	}
}