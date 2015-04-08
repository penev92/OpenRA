// #region Copyright & License Information
// /*
//  * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
//  * This file is part of OpenRA, which is free software. It is made
//  * available to you under the terms of the GNU General Public License
//  * as published by the Free Software Foundation. For more information,
//  * see COPYING.
//  */
// #endregion

using System;
using System.Linq;
using OpenRA.Mods.Common.AI;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Passes events to ModularAI. This trait is disabled if the owner is a human.")]
	public class ManagedByAIInfo : ITraitInfo
	{
		[Desc("The attacking category for this actor. You may want to attack a heavily fortified position with certain units." +
			"Examples: anti-infantry, hit-and-run, mechs, ..")]
		public readonly string[] AttackCategories = { };

		[Desc("Can attack actors whose TargetableTypes contains any member of this list.")]
		public readonly string[] CanAttackTargetables = { };

		[Desc("Can be targeted by actors whose AttackableTypes contains any member of this list.")]
		public readonly string[] TargetableTypes = { "any" };

		public object Create(ActorInitializer init) { return new ManagedByAI(init.Self, this); }
	}

	public class ManagedByAI : IDisabledTrait, INotifyIdle, INotifyBecomingIdle
	{
		public readonly ManagedByAIInfo Info;

		public bool IsTraitDisabled { get { return ai == null; } }

		readonly Lazy<ModularAI> ai;

		public ManagedByAI(Actor self, ManagedByAIInfo info)
		{
			Info = info;
			ai = Exts.Lazy(() => self.Owner.PlayerActor.TraitsImplementing<ModularAI>().FirstOrDefault(x => x.BotEnabled));
		}

		// It is important to note that `self` in the *AI logic
		// will be the affected actor, not the player actor

		public void OnBecomingIdle(Actor self)
		{
			if (!IsTraitDisabled && ai.Value != null)
				ai.Value.Notify<INotifyBecomingIdle>(n => n.OnBecomingIdle(self));
		}

		public void TickIdle(Actor self)
		{
			if (!IsTraitDisabled && ai.Value != null)
				ai.Value.Notify<INotifyIdle>(n => n.TickIdle(self));
		}
	}
}