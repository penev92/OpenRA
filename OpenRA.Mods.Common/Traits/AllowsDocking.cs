#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Drawing;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Allows other actors to dock with this actor," +
		"if they have the `Docks` trait or one of it's derivatives.")]
	public class AllowsDockingInfo : ITraitInfo
	{
		[Desc("A list of user-defined identifiers.")]
		public readonly HashSet<string> DockTypes = new HashSet<string>();

		// TODO: Take the angle into account.
		[Desc("Unit facing when docking, 0-255 counter-clock-wise." +
			"Leave the default value (-1) to allow any angle.")]
		public readonly int DockAngle = -1;

		[Desc("Docking cell relative to top-left cell.")]
		public readonly CVec DockOffset = CVec.Zero;

		[Desc("Waiting area relative to top-left cell.")]
		public readonly CVec WaitingOffset = CVec.Zero;

		public object Create(ActorInitializer init) { return new AllowsDocking(init.Self, this); }
	}

	public class AllowsDocking
	{
		public readonly AllowsDockingInfo Info;

		public Actor CurrentDocker { get; private set; }
		public CPos DockLocation { get { return self.Location + Info.DockOffset; } }
		public CPos WaitLocation { get { return self.Location + Info.WaitingOffset; } }

		readonly Actor self;
		readonly List<Actor> reserved;

		bool docked;

		public AllowsDocking(Actor self, AllowsDockingInfo info)
		{
			Info = info;
			this.self = self;

			reserved = new List<Actor>();
		}

		public Activity DockingSequence(Actor host, Actor docker)
		{
			return new DockSequence(host, docker, this);
		}

		public void Reserve(Actor docker)
		{
			if (!reserved.Contains(docker))
				reserved.Add(docker);
		}

		public void Unreserve(Actor docker)
		{
			if (reserved.Contains(docker))
				reserved.Remove(docker);
		}

		public void OnUndocked()
		{
			OrderDockerToRallyPoint();
			CurrentDocker = null;
			self.NotifyBlocker(DockLocation);
		}

		public void OnDocking(Actor docker)
		{
			CurrentDocker = docker;
		}

		public void OnDocked(Actor docker)
		{
			if (docked)
				return;

			CurrentDocker = docker;
			docked = true;
		}

		internal void OrderDockerToRallyPoint()
		{
			if (CurrentDocker == null)
				return;

			var rp = self.TraitOrDefault<RallyPoint>();
			if (rp == null)
				return;

			var nextActivity = CurrentDocker.GetCurrentActivity().NextActivity;
			var currentDocker = CurrentDocker;
			currentDocker.QueueActivity(false, new CallFunc(() =>
			{
				currentDocker.SetTargetLine(Target.FromCell(currentDocker.World, rp.Location), Color.Green);
				currentDocker.QueueActivity(new Move(currentDocker, rp.Location, self));
				currentDocker.QueueActivity(nextActivity);
			}));
		}
	}
}
