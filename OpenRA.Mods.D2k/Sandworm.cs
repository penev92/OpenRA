#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Mods.RA;
using OpenRA.Mods.RA.Move;
using OpenRA.Mods.RA.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.D2k
{
	class SandwormInfo : Requires<RenderUnitInfo>, Requires<MobileInfo>, IOccupySpaceInfo
	{
		readonly public int WanderMoveRadius = 20;
		readonly public string WormSignNotification = "WormSign";
		
		public object Create(ActorInitializer init) { return new Sandworm(this); }
	}

	class Sandworm : INotifyIdle
	{
		int ticksIdle;
		int effectiveMoveRadius;
		readonly int maxMoveRadius;

		public Sandworm(SandwormInfo info)
		{
			maxMoveRadius = info.WanderMoveRadius;
			effectiveMoveRadius = info.WanderMoveRadius;
		}

		// TODO: This copies AttackWander and builds on top of it. AttackWander should be revised.
		public void TickIdle(Actor self)
		{
			var target = self.CenterPosition + new WVec(0, -1024 * effectiveMoveRadius, 0).Rotate(WRot.FromFacing(self.World.SharedRandom.Next(255)));
			var targetCell = self.World.Map.CellContaining(target);

			if (!self.World.Map.Contains(targetCell))
			{
				// If MoveRadius is too big there might not be a valid cell to order the attack to (if actor is on a small island and can't leave)
				if (++ticksIdle % 10 == 0)      // completely random number
					effectiveMoveRadius--;

				return;  // We'll be back the next tick; better to sit idle for a few seconds than prolongue this tick indefinitely with a loop
			}

			self.World.IssueOrder(new Order("AttackMove", self, false) { TargetLocation = targetCell });

			ticksIdle = 0;
			effectiveMoveRadius = maxMoveRadius;
		}
	}
}
