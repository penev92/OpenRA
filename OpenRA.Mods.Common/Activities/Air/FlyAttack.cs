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
	public class FlyAttack : Activity
	{
		readonly Target target;
		readonly AttackPlane attackPlane;
		readonly IEnumerable<AmmoPool> ammoPools;
		Activity inner;
		int ticksUntilTurn = 50;

		public FlyAttack(Actor self, Target target)
		{
			this.target = target;
			attackPlane = self.TraitOrDefault<AttackPlane>();
			ammoPools = self.TraitsImplementing<AmmoPool>();
		}

		public override Activity Tick(Actor self)
		{
			if (!target.IsValidFor(self))
				return NextActivity;

			if (ammoPools != null)
				foreach (var pool in ammoPools)
				{
					// Skip every AmmoPool that SelfReloads or still has ammo
					if (pool.Info.SelfReloads || pool.HasAmmo())
						continue;

					return NextActivity;
				}

			if (attackPlane != null)
				attackPlane.DoAttack(self, target);

			if (inner == null)
			{
				if (IsCanceled)
					return NextActivity;

				if (target.IsInRange(self.CenterPosition, attackPlane.Armaments.Select(a => a.Weapon.MinRange).Min()))
					inner = Util.SequenceActivities(new FlyTimed(ticksUntilTurn), new Fly(self, target), new FlyTimed(ticksUntilTurn));
				else
					inner = Util.SequenceActivities(new Fly(self, target), new FlyTimed(ticksUntilTurn));
			}

			inner = Util.RunActivity(self, inner);

			return this;
		}

		public override void Cancel(Actor self)
		{
			if (!IsCanceled && inner != null)
				inner.Cancel(self);

			// NextActivity must always be set to null:
			base.Cancel(self);
		}
	}
}
