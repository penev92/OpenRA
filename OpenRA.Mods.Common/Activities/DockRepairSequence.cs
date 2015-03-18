#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class DockRepairSequence : Activity
	{
		protected enum State { Unreserved, Moving, Waiting, Docking, Docked, Undock }

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
			if (self.IsDead || !self.IsInWorld || host.IsDead || !host.IsInWorld)
				return NextActivity;

			if (IsCanceled)
				state = State.Undock;

			switch (state)
			{
				case State.Unreserved:
					Game.Debug("Reserving");
					state = State.Moving;
					return this;

				case State.Moving:
					if (self.Location == dockingForRepair.DockLocation)
						state = State.Docking;
					else
						if ((self.Location - dockingForRepair.DockLocation).Length < 2)
						{
							state = State.Waiting;
							return Util.SequenceActivities(new Move(self, dockingForRepair.WaitLocation), this);
						}

					Game.Debug("Moving");
					return this;

				case State.Waiting:
					if (dockingForRepair.CurrentDocker == null)
					{
						state = State.Moving;
						return Util.SequenceActivities(new Move(self, dockingForRepair.DockLocation), this);
					}
					return Util.SequenceActivities(new Wait(10), this);

				case State.Docking:
					if (dockingForRepair.RequestDock(self))
						state = State.Docked;

					Game.Debug("Docking");
					return this;

				case State.Docked:
					if (self.Location != dockingForRepair.DockLocation)
						state = State.Undock;

					if (--interval == 0)
					{
						DoRepair(self);
						interval = dockingForRepair.Info.Interval;
					}

					if (!docks.NeedsRepair())
						state = State.Undock;

					return this;

				case State.Undock:
					Game.Debug("Done");
					docks.Undock();
					dockingForRepair.Undock();
					return NextActivity;
			}

			throw new InvalidOperationException("Invalid dock state.");
		}

		void DoRepair(Actor self)
		{
			Game.Debug("Repairing");
			self.InflictDamage(host, -dockingForRepair.Info.HpPerStep, null);
		}
	}
}
