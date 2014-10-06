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

namespace OpenRA.Mods.RA
{
	public class OreRefineryInfo : RefineryInfo
	{
		public override object Create(ActorInitializer init) { return new OreRefinery(init.self, this); }
	}

	public class OreRefinery : Refinery
	{
		public OreRefinery (Actor self, OreRefineryInfo info) : base(self, info) { }

		public override Activity DockSequence(Actor harv, Actor self)
		{
			return new RAHarvesterDockSequence(harv, self, Info.DockAngle);
		}
	}
}
