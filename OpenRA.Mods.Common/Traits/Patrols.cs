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

		public List<CPos> PatrolWaypoints { get; private set; }

		public Patrols(Actor self, PatrolsInfo info)
		{
			Info = info;
			attackMove = self.Trait<AttackMove>();

			PatrolWaypoints = new List<CPos>();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			if (!attackMove.Info.MoveIntoShroud && order.Target.Type != TargetType.Invalid)
			{
				var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
				if (!self.Owner.Shroud.IsExplored(cell))
					return null;
			}

			if (order.OrderString != "AddPatrolWaypoint")
				return null;

			// return Info.Voice;
			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "AddPatrolWaypoint")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!attackMove.Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				if (!PatrolWaypoints.Contains(cell))
					PatrolWaypoints.Add(cell);

				if (PatrolWaypoints.Count == 1)
				{
					var assaultMoving = false; // TODO:
					self.QueueActivity(new PatrolActivity(self, PatrolWaypoints.ToArray(), Info.TargetLineColor, true, 0, assaultMoving));
				}
			}
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

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);

			if (mi.Button == ExpectedButton)
			{
				cell = world.Map.Clamp(cell);
				foreach (var a in subjects)
					yield return new Order("AddPatrolWaypoint", a.Actor, Target.FromCell(world, cell), queued);
			}
			else
				world.CancelInputMode();
		}
	}
}
