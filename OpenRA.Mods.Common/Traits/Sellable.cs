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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Traits.Buildings;

namespace OpenRA.Mods.Common.Traits
{

	[Desc("Actor can be sold")]
	public class SellableInfo : ITraitInfo
	{
		public readonly int RefundPercent = 50;
		public readonly string[] SellSounds = { };

		public object Create(ActorInitializer init) { return new Sellable(this); }
	}

	public class Sellable : IResolveOrder
	{
		readonly SellableInfo info;

		public Sellable(SellableInfo info) { this.info = info; }

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Sell")
				Sell(self);
		}

		public void Sell(Actor self)
		{
			var building = self.TraitOrDefault<Building>();
			if (building != null && !building.Lock())
				return;

			self.CancelActivity();

			foreach (var s in info.SellSounds)
				Sound.PlayToPlayer(self.Owner, s, self.CenterPosition);

			foreach (var ns in self.TraitsImplementing<INotifySold>())
				ns.Selling(self);

			var makeAnimation = self.TraitOrDefault<WithMakeAnimation>();
			if (makeAnimation != null)
				makeAnimation.Reverse(self, new Sell());
			else
				self.QueueActivity(new Sell());
		}
	}
}
