#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Provides access to the Patrol command.")]
	public class PatrolsInfo : TraitInfo, Requires<AttackMoveInfo>
	{
		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Color to use for the target line.")]
		public readonly Color TargetLineColor = Color.Yellow;

		public override object Create(ActorInitializer init) { return new Patrols(init.Self, this); }
	}

	public class Patrols : IResolveOrder, IOrderVoice
	{
		public readonly PatrolsInfo Info;
		readonly IMove move;
		readonly AttackMove attackMove;
		readonly List<CPos> patrolWaypoints;

		public List<CPos> PatrolWaypoints => patrolWaypoints;

		public Patrols(Actor self, PatrolsInfo info)
		{
			Info = info;
			move = self.Trait<IMove>();
			attackMove = self.Trait<AttackMove>();

			patrolWaypoints = new List<CPos>();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			if (!attackMove.Info.MoveIntoShroud && order.Target.Type != TargetType.Invalid)
			{
				var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
				if (!self.Owner.Shroud.IsExplored(cell))
					return null;
			}

			if (order.OrderString == "BeginPatrol" || order.OrderString == "BeginAssaultPatrol")
				return Info.Voice;

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "InitPatrol")
			{
				if (order.Queued)
				{
					var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
					if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
						return;

					if (!patrolWaypoints.Remove(cell))
						patrolWaypoints.Add(cell);
				}
				else
				{
					var startCell = self.World.Map.Clamp(self.World.Map.CellContaining(self.CenterPosition));
					if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(startCell))
						return;

					if (!patrolWaypoints.Remove(startCell))
						patrolWaypoints.Add(startCell);

					var targetCell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
					if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(targetCell))
						return;

					if (!patrolWaypoints.Remove(targetCell))
						patrolWaypoints.Add(targetCell);

					var assaultMoving = order.OrderString == "BeginAssaultPatrol";
					self.QueueActivity(new Patrol(self, patrolWaypoints.ToArray(), Info.TargetLineColor, true, 0, assaultMoving));
				}
			}
			else if (order.OrderString == "AddPatrolWaypoint")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				if (!patrolWaypoints.Remove(cell))
					patrolWaypoints.Add(cell);
			}
			else if (order.OrderString == "BeginPatrol" || order.OrderString == "BeginAssaultPatrol")
			{
				if (patrolWaypoints.Count < 2)
					return;

				if (!order.Queued)
					self.CancelActivity();

				var assaultMoving = order.OrderString == "BeginAssaultPatrol";
				self.QueueActivity(new Patrol(self, patrolWaypoints.ToArray(), Info.TargetLineColor, true, 0, assaultMoving));
				patrolWaypoints.Clear();
			}
			else
				patrolWaypoints.Clear();
		}
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

			if (mi.Button == expectedButton && !mi.Modifiers.HasModifier(Modifiers.Alt))
			{
				cell = world.Map.Clamp(cell);
				var pos = world.Map.CenterOfCell(cell);
				if (!waypoints.Remove(pos))
					waypoints.Add(pos);

				if (!started)
				{
					started = true;

					foreach (var a in subjects)
						yield return new Order("InitPatrol", a.Actor, Target.FromCell(world, cell), queued);
				}

				foreach (var a in subjects)
					yield return new Order("AddPatrolWaypoint", a.Actor, Target.FromCell(world, cell), queued);
			}
			else if (mi.Button == expectedButton && mi.Modifiers.HasModifier(Modifiers.Alt))
			{
				world.CancelInputMode();
				var order = mi.Modifiers.HasModifier(Modifiers.Ctrl) ? "BeginAssaultPatrol" : "BeginPatrol";

				foreach (var a in subjects)
					yield return new Order(order, a.Actor, queued);
			}
			else
			{
				world.CancelInputMode();
				yield return new Order(order, a.Actor, queued);
			}
		}

		public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
		{
			if (waypoints.Count < 2)
				yield break;

			yield return new TargetLineRenderable(waypoints, Color.Red);
		}
	}
}
