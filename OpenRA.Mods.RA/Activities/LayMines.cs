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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Activities
{
	// Assumes you have Minelayer on that unit
	public class LayMines : Activity
	{
		readonly Minelayer minelayer;
		readonly MinelayerInfo info;
		readonly IEnumerable<AmmoPool> ammoPools;

		public LayMines(Actor self)
		{
			minelayer = self.TraitOrDefault<Minelayer>();
			info = self.Info.Traits.Get<MinelayerInfo>();
			ammoPools = self.TraitsImplementing<AmmoPool>();
		}

		public override Activity Tick(Actor self)
		{
			if (IsCanceled)
				return NextActivity;

			var movement = self.Trait<IMove>();

			if (ammoPools != null)
				foreach (var pool in ammoPools)
				{
					// Skip every AmmoPool that does not have a matching name or still has ammo
					if (pool.Info.Name != info.AmmoPool || pool.HasAmmo())
						continue;

					if (pool.Info.Name == info.AmmoPool && !pool.HasAmmo())
					{
						// Rearm & repair at fix, then back out here to refill the minefield some more
						var buildings = info.RearmBuildings;
						var rearmTarget = self.World.Actors.Where(a => self.Owner.Stances[a.Owner] == Stance.Ally
							&& buildings.Contains(a.Info.Name))
							.ClosestTo(self);

						if (rearmTarget == null)
							return new Wait(20);

						return Util.SequenceActivities(
							new MoveAdjacentTo(self, Target.FromActor(rearmTarget)),
							movement.MoveTo(self.World.Map.CellContaining(rearmTarget.CenterPosition), rearmTarget),
							new Rearm(self),
							new Repair(rearmTarget),
							this);
					}
				}

			if (minelayer.Minefield.Contains(self.Location) && ShouldLayMine(self, self.Location))
			{
				LayMine(self);
				return Util.SequenceActivities(new Wait(20), this); // A little wait after placing each mine, for show
			}

			if (minelayer.Minefield.Length > 0)
			{
				// Don't get stuck forever here
				for (var n = 0; n < 20; n++)
				{
					var p = minelayer.Minefield.Random(self.World.SharedRandom);
					if (ShouldLayMine(self, p))
						return Util.SequenceActivities(movement.MoveTo(p, 0), this);
				}
			}

			// TODO: Return somewhere likely to be safe (near fix) so we're not sitting out in the minefield.
			return new Wait(20);	// nothing to do here
		}

		static bool ShouldLayMine(Actor self, CPos p)
		{
			// If there is no unit (other than me) here, we want to place a mine here
			return !self.World.ActorMap.GetUnitsAt(p).Any(a => a != self);
		}

		static void LayMine(Actor self)
		{
			var info = self.Info.Traits.Get<MinelayerInfo>();
			var ammoPools = self.TraitsImplementing<AmmoPool>();

			if (ammoPools != null)
				foreach (var pool in ammoPools)
				{
					if (pool.Info.Name != info.AmmoPool)
						continue;

					pool.TakeAmmo();
				}

			self.World.AddFrameEndTask(
				w => w.CreateActor(info.Mine, new TypeDictionary
				{
					new LocationInit(self.Location),
					new OwnerInit(self.Owner),
				}));
		}
	}
}
