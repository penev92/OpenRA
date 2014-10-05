#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.Common.Activities
{
	public class Rearm : Activity
	{
		readonly LimitedAmmo limitedAmmo;
		int ticksPerPip = 25 * 2;
		int remainingTicks = 25 * 2;

		public Rearm(Actor self)
		{
			limitedAmmo = self.TraitOrDefault<LimitedAmmo>();
			if (limitedAmmo != null)
				ticksPerPip = limitedAmmo.ReloadTimePerAmmo();
			remainingTicks = ticksPerPip;
		}

		public override Activity Tick(Actor self)
		{
			if (IsCanceled || limitedAmmo == null) return NextActivity;

			if (--remainingTicks == 0)
			{
				var hostBuilding = self.World.ActorMap.GetUnitsAt(self.Location)
					.FirstOrDefault(a => a.HasTrait<RenderBuilding>());

				if (hostBuilding == null || !hostBuilding.IsInWorld)
					return NextActivity;

				if (!limitedAmmo.GiveAmmo())
					return NextActivity;

				hostBuilding.Trait<RenderBuilding>().PlayCustomAnim(hostBuilding, "active");

				remainingTicks = limitedAmmo.ReloadTimePerAmmo();
			}

			return this;
		}
	}
}
