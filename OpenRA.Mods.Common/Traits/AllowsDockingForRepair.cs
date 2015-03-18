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
using System.Linq;
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

		[Desc("Waiting area relative to top-left cell.")]
		public readonly CVec WaitingOffset = CVec.Zero;

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
		public CPos WaitLocation { get { return self.Location + Info.WaitingOffset; } }

		List<Actor> reserved; 

		public AllowsDockingForRepair(Actor self, AllowsDockingForRepairInfo info)
		{
			Info = info;
			this.self = self;

			reserved = new List<Actor>();
		}

		public Activity DockingSequence(Actor host, Actor docker)
		{
			return new DockRepairSequence(host, docker, this);
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

		public bool RequestDock(Actor docker)
		{
			if (CurrentDocker != null)
			{
				docker.Trait<DocksForRepair>().MoveToWaitingArea(docker, self);
				return false;
			}

			if (docker.Location != DockLocation)
				return false;

			CurrentDocker = docker;

			Unreserve(docker);

			foreach (var actor in reserved)
				actor.Trait<DocksForRepair>().MoveToWaitingArea(actor, self);

			return true;
		}

		void NotifyReserved()
		{
			var actor = reserved.FirstOrDefault();
			if (actor == null)
				return;

			actor.Trait<DocksForRepair>().MoveInForDocking(actor, self);
		}

		public void Undock()
		{
			var rp = self.TraitOrDefault<RallyPoint>();
			if (rp == null)
			{
				CurrentDocker = null;
				return;
			}
			
			var nextActivity = CurrentDocker.GetCurrentActivity().NextActivity;
			CurrentDocker.QueueActivity(false, new CallFunc(() =>
				{
					CurrentDocker.SetTargetLine(Target.FromCell(self.World, rp.Location), Color.Green);
					CurrentDocker.QueueActivity(new Move(CurrentDocker, rp.Location));
					CurrentDocker.QueueActivity(nextActivity);

					CurrentDocker = null;
				}));

			self.NotifyBlocker(DockLocation);

			NotifyReserved();
		}
	}
}
