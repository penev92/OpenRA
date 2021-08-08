#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class PatrolActivity : Activity
	{
		readonly IMove move;
		readonly Patrols patrols;
		readonly int wait;
		readonly bool assaultMove;
		readonly Color targetLineColor;

		int currentWaypoint = 0;
		int direction = 1;
		bool hasStarted = false;

		public PatrolActivity(Actor self, CPos[] waypoints, Color targetLineColor, bool loop = true, int wait = 0, bool assaultMove = false)
			: this(self, waypoints, targetLineColor, () => !loop, wait, assaultMove)
		{
			ChildHasPriority = false;
		}

		public PatrolActivity(Actor self, CPos[] waypoints, Color targetLineColor, Func<bool> loopUntil, int wait = 0, bool assaultMove = false)
		{
			move = self.Trait<IMove>();
			patrols = self.Trait<Patrols>();

			this.targetLineColor = targetLineColor;

			this.wait = wait;
			this.assaultMove = assaultMove;
		}

		public override bool Tick(Actor self)
		{
			if (ChildActivity != null)
			{
				TickChild(self);
				return false;
			}

			if (IsCanceling)
				return true;

			if (!hasStarted)
			{
				patrols.AddStartingPoint(self.Location);
				hasStarted = true;
			}

			if (patrols.PatrolWaypoints.Count < 2)
				return true;

			var wpt = GetNextWaypoint();
			QueueChild(new AttackMoveActivity(self, () => move.MoveTo(wpt, 2), assaultMove));
			if (wait > 0)
				QueueChild(new Wait(wait));

			return false;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			for (var wpt = 0; wpt < patrols.PatrolWaypoints.Count; wpt++)
				yield return new TargetLineNode(Target.FromCell(self.World, patrols.PatrolWaypoints[wpt]), targetLineColor);
		}

		CPos GetNextWaypoint()
		{
			if (currentWaypoint + direction < 0 || currentWaypoint + direction >= patrols.PatrolWaypoints.Count)
				direction *= -1;

			currentWaypoint += direction;
			return patrols.PatrolWaypoints[currentWaypoint];
		}
	}
}
