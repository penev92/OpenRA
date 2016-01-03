#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor can dock with an actor with the `AllowsDocking` trait to recieve upgrades.")]
	class DocksForUpgradeInfo : DocksInfo, Requires<UpgradeManagerInfo>
	{
		[UpgradeGrantedReference, FieldLoader.Require]
		[Desc("The upgrades to grant while docked.")]
		public readonly string[] Upgrades = { };

		public override object Create(ActorInitializer init) { return new DocksForUpgrade(init.Self, this); }
	}

	class DocksForUpgrade : Docks
	{
		readonly DocksForUpgradeInfo info;

		UpgradeManager upgradeManager;

		public DocksForUpgrade(Actor self, DocksForUpgradeInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override void Created(Actor self)
		{
			upgradeManager = self.Trait<UpgradeManager>();
			base.Created(self);
		}

		public override void OnDocked()
		{
			if (Docked)
				return;

			Docked = true;
			foreach (var upgrade in info.Upgrades)
				upgradeManager.GrantUpgrade(self, upgrade, this);
		}

		public override void OnUndocking()
		{
			foreach (var upgrade in info.Upgrades)
				upgradeManager.RevokeUpgrade(self, upgrade, this);
		}
	}
}
