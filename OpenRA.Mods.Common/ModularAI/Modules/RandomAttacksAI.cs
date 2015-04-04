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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	public class RandomAttacksAIInfo : ITraitInfo, Requires<ModularAIInfo>
	{
		[Desc("Use `ManagedByAI` actors with `AttackingCategories` contained in this list. Leave empty for 'any'.")]
		public readonly string[] UseAttackingCategories = { "any" };

		[Desc("Ticks in between each scan for idle actors. Default equates to 2 seconds.")]
		public readonly int IdleScanFrequency = 25 * 2;

		public object Create(ActorInitializer init) { return new RandomAttacksAI(init.Self, this); }
	}

	public class RandomAttacksAI : IAILogic
	{
		readonly ModularAI ai;
		readonly World world;
		readonly RandomAttacksAIInfo info;

		int ticksSinceLastScan;

		public RandomAttacksAI(Actor self, RandomAttacksAIInfo info)
		{
			ai = self.Trait<ModularAI>();
			world = self.World;
			this.info = info;
			ticksSinceLastScan = info.IdleScanFrequency;

			ai.RegisterModule(this);
		}

		public void Tick(Actor self)
		{
			if (--ticksSinceLastScan > 0)
				return;

			ticksSinceLastScan = info.IdleScanFrequency;

			var idleAttackers = ai.GetIdleActors().Where(a =>
			{
				var aiInfo = a.Info.Traits.Get<ManagedByAIInfo>();
				if (!info.UseAttackingCategories.Intersect(aiInfo.AttackCategories).Any())
					return false;

				return a.HasTrait<AttackBase>();
			});

			foreach (var attacker in idleAttackers)
			{
				var target = ClosestTargetableActor(attacker, attacker.Info.Traits.Get<ManagedByAIInfo>());
				if (target == null || target.IsDead || !target.IsInWorld)
				{
					ai.Debug("Target is null, dead, or !inWorld");
					continue;
				}

				var cell = world.Map.CellContaining(target.CenterPosition);
				world.IssueOrder(new Order("AttackMove", attacker, false)
				{
					TargetLocation = cell
				});
			}
		}

		protected virtual Actor ClosestTargetableActor(Actor attacker, ManagedByAIInfo attackerAIInfo)
		{
			var attack = attacker.Trait<AttackBase>();

			var targets = world.Actors.Where(a =>
			{
				if (a == null || a.IsDead || !a.IsInWorld)
					return false;

				var position = a.TraitOrDefault<IPositionable>();
				if (position == null)
					return false;

				if (a.AppearsFriendlyTo(attacker))
					return false;

				if (!attacker.Owner.Shroud.IsExplored(a))
					return false;

				/* TODO: Handle frozen actors
				if (world.FogObscures(a))
					test if exist,
					if not get frozen actor data,
					determine if we should try to attack it
				*/

				if (!a.HasTrait<TargetableUnit>())
					return false;

				var aiInfo = a.Info.Traits.GetOrDefault<ManagedByAIInfo>();
				if (aiInfo == null)
					return false;

				var typesMatch = attackerAIInfo.CanAttackTargetables.Intersect(aiInfo.TargetableTypes).Any();
				if (!typesMatch)
					return false;

				return attack.HasAnyValidWeapons(Target.FromActor(a));
			});

			return targets.ClosestTo(attacker);
		}
	}
}