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
	public class Patrol : Activity
	{
		readonly IMove move;
		readonly Patrols patrols;
		readonly CPos[] waypoints;
		readonly Func<bool> loopUntil;
		readonly int wait;
		readonly bool assaultMove;
		readonly Color targetLineColor;

		int currentWaypoint = 0;
		int direction = 1;

		public Patrol(Actor self, CPos[] waypoints, Color targetLineColor, bool loop = true, int wait = 0, bool assaultMove = false)
			: this(self, waypoints, targetLineColor, () => !loop, wait, assaultMove)
		{
			ChildHasPriority = false;
		}

		public Patrol(Actor self, CPos[] waypoints, Color targetLineColor, Func<bool> loopUntil, int wait = 0, bool assaultMove = false)
		{
			move = self.Trait<IMove>();
			patrols = self.Trait<Patrols>();

			this.waypoints = waypoints;
			this.targetLineColor = targetLineColor;

			this.loopUntil = loopUntil;
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
			for (var wpt = 0; wpt < waypoints.Length; wpt++)
				yield return new TargetLineNode(Target.FromCell(self.World, waypoints[wpt]), targetLineColor);

			if (waypoints.Length > 0 && !loopUntil())
				yield return new TargetLineNode(Target.FromCell(self.World, waypoints[0]), targetLineColor);
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
