#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class DockRepairSequence : Activity
	{
		protected enum State { Unreserved, Moving, Docking, Docked, Done }

		readonly Actor host;
		readonly IMove movement;
		//readonly CPos dockLocation;
		readonly DocksForRepair docks;
		readonly AllowsDockingForRepair dockingForRepair;

		int interval;
		
		State state;

		public DockRepairSequence(Actor host, Actor docker, AllowsDockingForRepair dockingForRepair)
		{
			this.host = host;
			state = State.Unreserved;
			movement = docker.Trait<IMove>();
			docks = docker.Trait<DocksForRepair>();
			this.dockingForRepair = dockingForRepair;

			interval = dockingForRepair.Info.Interval;
			//dockLocation = host.Location + dockingForRepair.Info.DockOffset;
		}

		public override Activity Tick(Actor self)
		{
			if (IsCanceled || self.IsDead || !self.IsInWorld || host.IsDead || !host.IsInWorld)
				return NextActivity;

			if (state == State.Unreserved)
			{
				Game.Debug("Reserving");
				state = State.Moving;
				return this;
			}

			if (state == State.Moving)
			{
				if (self.Location == dockingForRepair.DockLocation)
					state = State.Docking;

				Game.Debug("Moving");
				return this;
			}

			if (state == State.Docking)
			{
				if (dockingForRepair.RequestDock(self))
					state = State.Docked;

				Game.Debug("Docking");
				return this;
			}

			if (state == State.Docked)
			{
				//Game.Debug("Docked");
				if (--interval == 0)
				{
					DoRepair(self);
					interval = dockingForRepair.Info.Interval;
				}

				if (!docks.NeedsRepair())
					state = State.Done;

				return this;
			}

	        if (state == State.Done)
			{
				Game.Debug("Done");
			    dockingForRepair.Undock();
			}



			return NextActivity;
			//return Util.SequenceActivities(NextActivity, this);
		}

		void DoRepair(Actor self)
		{
			Game.Debug("Repairing");
			self.InflictDamage(host, -dockingForRepair.Info.HpPerStep, null);
		}
	}
}
