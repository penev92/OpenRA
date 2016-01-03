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
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class DockSequence : Activity
	{
		protected enum State { Unreserved, Moving, Waiting, Docking, Docked, Undocking, Undocked }

		readonly Actor host;
		readonly Actor docker;
		readonly Docks[] dockerTraits;
		readonly AllowsDocking dock;
		readonly string[] enabledDockTypes;

		State state;

		public DockSequence(Actor host, Actor docker, AllowsDocking dock)
		{
			this.host = host;
			this.dock = dock;
			this.docker = docker;

			var allDockerTraits = docker.TraitsImplementing<Docks>().ToArray();

			// All dockTypes that may come into play.
			enabledDockTypes = allDockerTraits.SelectMany(x => x.Info.AcceptableDockTypes)
				.Intersect(dock.Info.DockTypes).ToArray();
			
			// All Docks* traits that will be used, based on dockType matching.
			dockerTraits = allDockerTraits.Where(x => x.Info.AcceptableDockTypes.Overlaps(enabledDockTypes)).ToArray();

			state = State.Unreserved;
		}

		public override Activity Tick(Actor self)
		{
			if (self.IsDead || !self.IsInWorld || host.IsDead || !host.IsInWorld)
				return NextActivity;

			switch (state)
			{
				case State.Unreserved:
					state = State.Moving;
					return this;

				case State.Moving:
					if (self.Location == dock.DockLocation)
					{
						state = State.Docking;
					}
					else if ((self.Location - dock.DockLocation).Length < 2)
					{
						state = State.Waiting;
						return Util.SequenceActivities(new Move(self, dock.WaitLocation), this);
					}

					return this;

				case State.Waiting:
					if (dock.CurrentDocker == null)
					{
						state = State.Moving;
						return Util.SequenceActivities(new Move(self, dock.DockLocation), this);
					}

					return Util.SequenceActivities(new Wait(10), this);

				case State.Docking:
					if (Docks.AttemptDock(docker, dock))
					{
						foreach (var dTrait in dockerTraits)
							dTrait.OnDocking();

						dock.OnDocking(docker);
						state = State.Docked;
						return this;
					}

					return Util.SequenceActivities(new Move(self, dock.WaitLocation), this);

				case State.Docked:
					var shouldUndock = true;
					foreach (var dTrait in dockerTraits)
					{
						dTrait.OnDocked();
						shouldUndock &= dTrait.ShouldUndock();
					}

					if (self.Location != dock.DockLocation || shouldUndock)
						state = State.Undocking;

					return this;

				case State.Undocking:
					foreach (var dTrait in dockerTraits)
						dTrait.OnUndocking();

					state = State.Undocked;

					return this;

				case State.Undocked:
					foreach (var dTrait in dockerTraits)
						dTrait.OnUndocked();

					dock.OnUndocked();

					return NextActivity;
			}

			throw new InvalidOperationException("Invalid dock state.");
		}

		public override void Cancel(Actor self)
		{
			state = State.Undocking;
			dock.Unreserve(self);
			base.Cancel(self);
		}
	}
}
