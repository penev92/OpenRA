#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor can dock with an actor with the `AllowsDocking` trait to be repaired.")]
	class DocksForRepairInfo : DocksInfo, Requires<HealthInfo>
	{
		// TODO: Take the cost into account.
		[Desc("Cost of the actor's value in % for full repair.")]
		public readonly int ValuePercentage = 0;

		[Desc("Time (in ticks) between two repair steps.")]
		public readonly int Interval = 0;

		[Desc("By how much is this actor repaired per step.")]
		public readonly int HpPerStep = 1;

		[Desc("The sound played when starting to repair a unit.")]
		public readonly string StartRepairingNotification = null;

		[Desc("The sound played when repairing a unit is done.")]
		public readonly string FinishRepairingNotification = null;

		public override object Create(ActorInitializer init) { return new DocksForRepair(init.Self, this); }
	}

	class DocksForRepair : Docks
	{
		readonly DocksForRepairInfo info;

		Health health;
		int remainingTicks;
		INotifyRepair[] notifyRepairTraits;

		public DocksForRepair(Actor self, DocksForRepairInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override void Created(Actor self)
		{
			health = self.Trait<Health>();
			base.Created(self);
		}

		internal override bool ShouldDock()
		{
			return health.DamageState > DamageState.Undamaged;
		}

		public override bool ShouldUndock()
		{
			return health.DamageState == DamageState.Undamaged;
		}

		public override void OnDocking()
		{
			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner,
				"Speech", info.StartRepairingNotification, self.Owner.Faction.InternalName);

			remainingTicks = 0;
			notifyRepairTraits = Dock.TraitsImplementing<INotifyRepair>().ToArray();
		}

		public override void OnDocked()
		{
			if (--remainingTicks > 0)
				return;

			if (!Docked)
				Docked = true;

			foreach (var trait in notifyRepairTraits)
				trait.Repairing(self, Dock);

			self.InflictDamage(Dock, -info.HpPerStep, null);
			remainingTicks = info.Interval;
		}

		public override void OnUndocking()
		{
			if (ShouldUndock())
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner,
					"Speech", info.FinishRepairingNotification, self.Owner.Faction.InternalName);
		}
	}
}
