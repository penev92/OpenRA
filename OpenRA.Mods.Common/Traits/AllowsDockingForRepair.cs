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
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("")]
	public class AllowsDockingForRepairInfo : ITraitInfo
	{
		[Desc("Unit facing when docking, 0-255 counter-clock-wise.")]
		public readonly int DockAngle = 0;

		[Desc("Docking cell relative to top-left cell.")]
		public readonly CVec DockOffset = CVec.Zero;

		public readonly int HpPerStep = 10;

		[Desc("Time (in ticks) between two repair steps.")]
		public readonly int Interval = 24;

		public object Create(ActorInitializer init) { return new AllowsDockingForRepair(init.Self, this); }
	}

	public class AllowsDockingForRepair
	{
		public readonly AllowsDockingForRepairInfo Info;

		readonly Actor self;

		public Actor CurrentDocker { get; private set; }

		public CPos DockLocation { get { return self.Location + Info.DockOffset; } }

		public AllowsDockingForRepair(Actor self, AllowsDockingForRepairInfo info)
		{
			Info = info;
			this.self = self;
		}

		public Activity DockingSequence(Actor host, Actor docker)
		{
			return new DockRepairSequence(host, docker, this);
		}

		public bool RequestDock(Actor docker)
		{
			if (CurrentDocker != null)
				return false;

			CurrentDocker = docker;
			return true;
		}

		public void Undock()
		{
			CurrentDocker = null;
		}
	}
}
