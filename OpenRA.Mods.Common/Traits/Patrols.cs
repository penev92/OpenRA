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

using System.Collections.Generic;
using System.Linq;
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
		readonly AttackMove attackMove;

		public List<CPos> PatrolWaypoints { get; }

		public Patrols(Actor self, PatrolsInfo info)
		{
			Info = info;
			attackMove = self.Trait<AttackMove>();

			PatrolWaypoints = new List<CPos>();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "DoPatrol" ? Info.Voice : null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "DoPatrol")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				if (!order.Queued)
					PatrolWaypoints.Clear();

				if (!PatrolWaypoints.Contains(cell))
					PatrolWaypoints.Add(cell);

				if (PatrolWaypoints.Count == 1)
				{
					var assaultMoving = false; // TODO:
					self.QueueActivity(order.Queued, new PatrolActivity(self, PatrolWaypoints.ToArray(), Info.TargetLineColor, true, 0, assaultMoving));
				}
			}

			// Explicitly also clearing/cancelling on queued orders as well as non-queued because otherwise they won't ever be executed.
			else
				PatrolWaypoints.Clear();
		}

		public void AddStartingPoint(CPos start)
		{
			PatrolWaypoints.Insert(0, start);
		}
	}

	public class PatrolOrderGenerator : AttackMoveOrderGenerator
	{
		public PatrolOrderGenerator(IEnumerable<Actor> subjects, MouseButton button)
			: base(subjects, button) { }

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, MouseInput mi)
		{
			if (mi.Button == ExpectedButton)
			{
				var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
				if (!queued)
					world.CancelInputMode();

				var orderName = "DoPatrol";

				// Cells outside the playable area should be clamped to the edge for consistency with move orders.
				cell = world.Map.Clamp(cell);
				yield return new Order(orderName, null, Target.FromCell(world, cell), queued, null, subjects.Select(s => s.Actor).ToArray());
			}
		}
	}
}
