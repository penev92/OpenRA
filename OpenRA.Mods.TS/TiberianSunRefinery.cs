#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;
using OpenRA.Mods.Common.Traits.Buildings;

namespace OpenRA.Mods.TS
{
	public class TiberianSunRefineryInfo : RefineryInfo
	{
		public override object Create(ActorInitializer init) { return new TiberianSunRefinery(init.self, this); }
	}

	public class TiberianSunRefinery : Refinery
	{
		public TiberianSunRefinery(Actor self, TiberianSunRefineryInfo info) : base(self, info) { }

		public override Activity DockSequence(Actor harv, Actor self)
		{
			return new VoxelHarvesterDockSequence(harv, self);
		}
	}
}
