#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Provides access to the attack-move command, which will make the actor automatically engage viable targets while moving to the destination.")]
	public sealed class AttackMoveInfo : TraitInfo, Requires<IMoveInfo>
	{
		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Color to use for the target line.")]
		public readonly Color TargetLineColor = Color.OrangeRed;

		[GrantedConditionReference]
		[Desc("The condition to grant to self while an attack-move is active.")]
		public readonly string AttackMoveCondition = null;

		[GrantedConditionReference]
		[Desc("The condition to grant to self while an assault-move is active.")]
		public readonly string AssaultMoveCondition = null;

		[Desc("Can the actor be ordered to move in to shroud?")]
		public readonly bool MoveIntoShroud = true;

		[CursorReference]
		public readonly string AttackMoveCursor = "attackmove";

		[CursorReference]
		public readonly string AttackMoveBlockedCursor = "attackmove-blocked";

		[CursorReference]
		public readonly string AssaultMoveCursor = "assaultmove";

		[CursorReference]
		public readonly string AssaultMoveBlockedCursor = "assaultmove-blocked";

		public override object Create(ActorInitializer init) { return new AttackMove(init.Self, this); }
	}

	public sealed class AttackMove : IResolveOrder, IOrderVoice
	{
		public readonly AttackMoveInfo Info;
		readonly IMove move;

		public AttackMove(Actor self, AttackMoveInfo info)
		{
			move = self.Trait<IMove>();
			Info = info;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			if (!Info.MoveIntoShroud && order.Target.Type != TargetType.Invalid)
			{
				var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
				if (!self.Owner.Shroud.IsExplored(cell))
					return null;
			}

			if (order.OrderString == "AttackMove" || order.OrderString == "AssaultMove")
				return Info.Voice;

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "AttackMove" || order.OrderString == "AssaultMove")
			{
				if (!order.Target.IsValidFor(self))
					return;

				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				var targetLocation = move.NearestMoveableCell(cell);
				var assaultMoving = order.OrderString == "AssaultMove";

				// TODO: this should scale with unit selection group size.
				self.QueueActivity(order.Queued, new AttackMoveActivity(self, () => move.MoveTo(targetLocation, 8, targetLineColor: Info.TargetLineColor), assaultMoving));
				self.ShowTargetLines();
			}
		}
	}

	public class AttackMoveOrderGenerator : UnitOrderGenerator
	{
		protected TraitPair<AttackMove>[] subjects;

		protected readonly MouseButton ExpectedButton;

		public AttackMoveOrderGenerator(IEnumerable<Actor> subjects, MouseButton button)
		{
			ExpectedButton = button;

			this.subjects = subjects.Where(a => !a.IsDead)
				.SelectMany(a => a.TraitsImplementing<AttackMove>()
					.Select(am => new TraitPair<AttackMove>(a, am)))
				.ToArray();
		}

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button != ExpectedButton)
				world.CancelInputMode();

			return OrderInner(world, cell, mi);
		}

		protected virtual IEnumerable<Order> OrderInner(World world, CPos cell, MouseInput mi)
		{
			if (mi.Button == ExpectedButton)
			{
				var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
				if (!queued)
					world.CancelInputMode();

				var orderName = mi.Modifiers.HasModifier(Modifiers.Ctrl) ? "AssaultMove" : "AttackMove";

				// Cells outside the playable area should be clamped to the edge for consistency with move orders.
				cell = world.Map.Clamp(cell);
				yield return new Order(orderName, null, Target.FromCell(world, cell), queued, null, subjects.Select(s => s.Actor).ToArray());
			}
		}

		public override void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			subjects = selected.Where(s => !s.IsDead).SelectMany(a => a.TraitsImplementing<AttackMove>()
					.Select(am => new TraitPair<AttackMove>(a, am)))
				.ToArray();

			// AttackMove doesn't work without AutoTarget, so require at least one unit in the selection to have it
			if (!subjects.Any(s => s.Actor.Info.HasTraitInfo<AutoTargetInfo>()))
				world.CancelInputMode();
		}

		public override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var isAssaultMove = mi.Modifiers.HasModifier(Modifiers.Ctrl);

			var subject = subjects.FirstOrDefault();
			if (subject.Actor != null)
			{
				var info = subject.Trait.Info;
				if (world.Map.Contains(cell))
				{
					var explored = subject.Actor.Owner.Shroud.IsExplored(cell);
					var cannotMove = subjects.FirstOrDefault(a => !a.Trait.Info.MoveIntoShroud).Trait;
					var blocked = !explored && cannotMove != null;

					if (isAssaultMove)
						return blocked ? cannotMove.Info.AssaultMoveBlockedCursor : info.AssaultMoveCursor;

					return blocked ? cannotMove.Info.AttackMoveBlockedCursor : info.AttackMoveCursor;
				}

				if (isAssaultMove)
					return info.AssaultMoveBlockedCursor;
				else
					return info.AttackMoveBlockedCursor;
			}

			return null;
		}

		public override bool InputOverridesSelection(World world, int2 xy, MouseInput mi)
		{
			// Custom order generators always override selection
			return true;
		}

		public override bool ClearSelectionOnLeftClick => false;
	}

	public class PatrolOrderGenerator : AttackMoveOrderGenerator
	{
		readonly List<WPos> waypoints;
		bool started;

		public PatrolOrderGenerator(IEnumerable<Actor> subjects, MouseButton button)
			: base(subjects, button)
		{
			waypoints = new List<WPos>();
		}

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);

			if (mi.Button == ExpectedButton && !mi.Modifiers.HasModifier(Modifiers.Alt))
			{
				if (!started)
				{
					started = true;

					foreach (var a in subjects)
						yield return new Order("InitPatrol", a.Actor, queued);
				}

				cell = world.Map.Clamp(cell);
				var pos = world.Map.CenterOfCell(cell);
				if (!waypoints.Remove(pos))
					waypoints.Add(pos);

				foreach (var a in subjects)
					yield return new Order("AddPatrolWaypoint", a.Actor, Target.FromCell(world, cell), queued);
			}
			else if (mi.Button == ExpectedButton && mi.Modifiers.HasModifier(Modifiers.Alt))
			{
				world.CancelInputMode();
				var order = mi.Modifiers.HasModifier(Modifiers.Ctrl) ? "BeginAssaultPatrol" : "BeginPatrol";

				foreach (var a in subjects)
					yield return new Order(order, a.Actor, queued);
			}
			else
				world.CancelInputMode();
		}

		public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
		{
			if (waypoints.Count < 2)
				yield break;

			yield return new TargetLineRenderable(waypoints, Color.Red);
		}
	}
}
