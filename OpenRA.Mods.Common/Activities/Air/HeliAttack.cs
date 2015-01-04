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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class HeliAttack : Activity
	{
		readonly Target target;
		readonly Helicopter helicopter;
		readonly AttackHeli attackHeli;
		readonly IEnumerable<AmmoPool> ammoPools;

		public HeliAttack(Actor self, Target target)
		{
			this.target = target;
			helicopter = self.Trait<Helicopter>();
			attackHeli = self.Trait<AttackHeli>();
			ammoPools = self.TraitsImplementing<AmmoPool>();
		}

		public override Activity Tick(Actor self)
		{
			if (IsCanceled || !target.IsValidFor(self))
				return NextActivity;

			if (ammoPools != null)
				foreach (var pool in ammoPools)
				{
					// Skip every AmmoPool that SelfReloads or still has ammo
					if (pool.Info.SelfReloads || pool.HasAmmo())
						continue;

					return Util.SequenceActivities(new HeliReturn(), NextActivity);
				}

			var dist = target.CenterPosition - self.CenterPosition;

			// Can rotate facing while ascending
			var desiredFacing = Util.GetFacing(dist, helicopter.Facing);
			helicopter.Facing = Util.TickFacing(helicopter.Facing, desiredFacing, helicopter.ROT);

			if (HeliFly.AdjustAltitude(self, helicopter, helicopter.Info.CruiseAltitude))
				return this;

			// Fly towards the target
			if (!target.IsInRange(self.CenterPosition, attackHeli.GetMaximumRange()))
				helicopter.SetPosition(self, helicopter.CenterPosition + helicopter.FlyStep(desiredFacing));

			attackHeli.DoAttack(self, target);

			return this;
		}
	}
}
