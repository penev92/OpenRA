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
		protected enum State { Unreserved, Waiting, Docking, Docked, Undock }

		readonly Actor host;
		readonly IMove movement;
		//readonly CPos dockLocation;
		readonly DocksForRepair docker;
		readonly AllowsDockingForRepair dock;

		int interval;
		
		State state;

		public DockRepairSequence(Actor host, Actor docker, AllowsDockingForRepair dock)
		{
			this.host = host;
			this.dock = dock;
			this.docker = docker.Trait<DocksForRepair>();
			
			state = State.Unreserved;
			movement = docker.Trait<IMove>();

			interval = dock.Info.Interval;
		}

		public override Activity Tick(Actor self)
		{
			if (self.IsDead || !self.IsInWorld || host.IsDead || !host.IsInWorld)
				return NextActivity;

			if (IsCanceled)
			{
				if (state < State.Docked)
					return NextActivity;

				state = State.Undock;
			}

			switch (state)
			{
				case State.Unreserved:
					Game.Debug("Reserving");
					//dock.Reserve(self);
					state = State.Waiting;
					return this;

				//case State.Moving:
					//if (self.Location == dock.DockLocation)
					//    state = State.Docking;
					//else
					//    if ((self.Location - dock.DockLocation).Length < 2)
					//    {
					//        state = State.Waiting;
					//        return Util.SequenceActivities(new Move(self, dock.WaitLocation), this);
					//    }

					//Game.Debug("Moving");
					//return this;

				case State.Waiting:
					if (dock.CurrentDocker == null)
					{
						//state = State.Moving;
						//return movement.MoveTo(dock.DockLocation, host);
						state = State.Docking;
						return Util.SequenceActivities(movement.MoveTo(dock.DockLocation, host), this);
					}
					return Util.SequenceActivities(new Wait(10), this);

				case State.Docking:
					if (dock.RequestDock(self))
					{
						state = State.Docked;
						docker.Dock(dock);
					}

					Game.Debug("Docking");
					return this;

				case State.Docked:
					if (self.Location != dock.DockLocation)
						state = State.Undock;

					if (--interval == 0)
					{
						DoRepair(self);
						interval = dock.Info.Interval;
					}

					if (!docker.NeedsRepair())
						state = State.Undock;

					return this;

				case State.Undock:
					Game.Debug("Done");
					docker.Undock();
					dock.Undock();
					return NextActivity;
			}

			throw new InvalidOperationException("Invalid dock state.");
		}

		void DoRepair(Actor self)
		{
			Game.Debug("Repairing");
			self.InflictDamage(host, -dock.Info.HpPerStep, null);
		}
	}
}
