#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.RA.Move;
using OpenRA.Mods.RA.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.D2k
{
	public enum AttackState { Burrowed, EmergingAboveGround, ReturningUndergrown }

	class SwallowActor : Activity
	{
		readonly Actor target;
		readonly Mobile mobile;
		readonly Sandworm sandworm;
		readonly WeaponInfo weapon;

		int countdown;
		AttackState stance = AttackState.Burrowed;

		// TODO: Random numbers to make it look ok
		[Desc("The number of ticks it takes to return underground.")]
		const int ReturnTime = 60;            
		[Desc("The number of ticks it takes to get in place under the target to attack.")]
		const int AttackTime = 30;

		public SwallowActor(Actor self, Actor target, WeaponInfo weapon)
		{
			if (!target.HasTrait<Mobile>())
				throw new InvalidOperationException("SwallowActor requires a target actor with the Mobile trait");

			this.target = target;
			this.weapon = weapon;
			mobile = self.TraitOrDefault<Mobile>();
			sandworm = self.TraitOrDefault<Sandworm>();
			countdown = AttackTime;
		}

		bool WormAttack(Actor worm)
		{
			var targetLocation = target.Location;

			var lunch = worm.World.ActorMap.GetUnitsAt(targetLocation)
							.Except(new[] { worm })
							.Where(t => weapon.IsValidAgainst(t, worm));
			if (!lunch.Any())
				return false;

			stance = AttackState.EmergingAboveGround;

			lunch.Do(t => t.World.AddFrameEndTask(_ => { t.World.Remove(t); t.Kill(t); }));          // Dispose of the evidence (we don't want husks)
			
			mobile.SetPosition(worm, targetLocation);
			PlayAttackAnimation(worm);

			return true;
		}

		void PlayAttackAnimation(Actor self)
		{
			var renderUnit = self.Trait<RenderUnit>();
			renderUnit.PlayCustomAnim(self, "sand");
			renderUnit.PlayCustomAnim(self, "mouth");
		}

		public override Activity Tick(Actor self)
		{
			if (countdown > 0)
			{
				countdown--;
				return this;
			}

			if (stance == AttackState.ReturningUndergrown)    // Wait for the worm to get back underground
			{
				#region DisappearToMapEdge

				// More random numbers used for min and max bounds
				var rand = self.World.SharedRandom.Next(200, 400);
				if (rand % 2 == 0) // There is a 50-50 chance that the worm would just go away
				{
					self.CancelActivity();
					self.World.AddFrameEndTask(w => w.Remove(self));
					var wormManager = self.World.WorldActor.TraitOrDefault<WormManager>();
					if (wormManager != null)
						wormManager.DecreaseWorms();
				}

				#endregion
				
				// TODO: If the worm did not disappear, make the animation reappear here

				return NextActivity;
			}

			if (stance == AttackState.Burrowed)   // Wait for the worm to get in position
			{
				// TODO: Make the worm animation (currenty the lightning) disappear here

				// This is so that the worm cancels an attack against a target that has reached solid rock
				if (sandworm == null || mobile == null || mobile.MovementSpeedForCell(self, target.Location) == 0)
					return NextActivity;

				var success = WormAttack(self);
				if (!success)
					return NextActivity;

				countdown = ReturnTime;
				stance = AttackState.ReturningUndergrown;
			}

			return this;
		}
	}
}
