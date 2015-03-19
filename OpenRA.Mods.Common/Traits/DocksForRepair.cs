#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor can dock with an actor with the `AllowsDockingForRepair` trait to be repaired.")]
	class DocksForRepairInfo : ITraitInfo, Requires<IMoveInfo>, Requires<HealthInfo>
	{
		public readonly string[] RepairBuildings = { "fix" };

		public object Create(ActorInitializer init) { return new DocksForRepair(init.Self, this); }
	}

	class DocksForRepair : IIssueOrder, IResolveOrder, INotifyActivitiesCancelled
	{
		readonly IMove move;
		readonly Health health;
		readonly DocksForRepairInfo info;
		
		Actor self;
		AllowsDockingForRepair dock;
		public bool HasReservation;

		public DocksForRepair(Actor self, DocksForRepairInfo info)
		{
			this.self = self;
			this.info = info;
			move = self.Trait<IMove>();
			health = self.Trait<Health>();
		}

		bool CanRepairAt(Actor target)
		{
			return info.RepairBuildings.Contains(target.Info.Name);
		}

		public bool NeedsRepair()
		{
			return health.DamageState > DamageState.Undamaged;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				yield return new EnterAlliedActorTargeter<Building>("Repair", 5, CanRepairAt, _ => NeedsRepair());
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "Repair")
				return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "Repair")
				return;

			if (!CanRepairAt(order.TargetActor) || !NeedsRepair())
				return;

			var host = order.TargetActor;

			MoveInForDocking(self, host);
		}

		public void MoveInForDocking(Actor self, Actor host)
		{
			self.CancelActivity();

			dock = host.Trait<AllowsDockingForRepair>();
			 var dockSequence = dock.DockingSequence(host, self);
			 dock.Reserve(self);

			 //self.QueueActivity(new MoveAdjacentTo(self, Target.FromActor(host)));
			 self.QueueActivity(move.MoveTo(dock.DockLocation, host));
			self.QueueActivity(dockSequence);
		}

		public void MoveToWaitingArea(Actor self, Actor host)
		{
			self.CancelActivity();

			dock = host.Trait<AllowsDockingForRepair>();
			var dockSequence = dock.DockingSequence(host, self);
			dock.Reserve(self);

			self.QueueActivity(new MoveAdjacentTo(self, Target.FromActor(host)));
			self.QueueActivity(move.MoveTo(dock.WaitLocation, host));
			self.QueueActivity(dockSequence);
		}

		public void Dock(AllowsDockingForRepair host)
		{
		}

		public void Undock()
		{
			dock = null;
		}

		public void OnCancelled()
		{
			if (dock == null)
				return;

			dock.Unreserve(self);
			dock = null;
		}
	}
}
