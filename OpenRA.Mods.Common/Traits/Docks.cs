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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor can dock with an actor with the `AllowsDocking` trait." + 
		"This is a base trait for other docking traits and has no practical usage.")]
	class DocksInfo : ITraitInfo, Requires<IMoveInfo>
	{
		[Desc("A list of user-defined identifiers.")]
		public readonly HashSet<string> AcceptableDockTypes = new HashSet<string>();

		public virtual object Create(ActorInitializer init) { return new Docks(init.Self, this); }
	}

	class Docks : IIssueOrder, IResolveOrder, ISync, INotifyCreated
	{
		public readonly DocksInfo Info;
		internal readonly Actor self;

		IMove move;

		[Sync] internal bool Docked;
		[Sync] internal Actor Dock;

		public Docks(Actor self, DocksInfo info)
		{
			this.self = self;
			Info = info;
		}

		public virtual void Created(Actor self)
		{
			move = self.Trait<IMove>();
		}

		/// <summary>
		/// Can this actor dock with the given target actor.
		/// </summary>
		/// <param name="target">The potential dock target sto check.</param>
		internal virtual bool CanDockAt(Actor target)
		{
			var dock = target.TraitOrDefault<AllowsDocking>();
			return dock != null && dock.Info.DockTypes.Overlaps(Info.AcceptableDockTypes);
		}

		/// <summary>
		/// Does the actor have a reason for docking?
		/// </summary>
		internal virtual bool ShouldDock()
		{
			return true;
		}

		/// <summary>
		/// Is remaining docked still necessary?
		/// </summary>
		/// <returns>Returns TRUE if the actor no longer needs to remain docked.</returns>
		public virtual bool ShouldUndock()
		{
			return true;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				yield return new EnterAlliedActorTargeter<BuildingInfo>("Dock", 5, CanDockAt, _ => ShouldDock());
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "Dock")
				return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "Dock")
				return;

			if (!CanDockAt(order.TargetActor) || !ShouldDock())
				return;

			var host = order.TargetActor;

			MoveInForDocking(self, host);
		}

		public static bool AttemptDock(Actor self, AllowsDocking dockTrait)
		{
			if (dockTrait.CurrentDocker != null)
				return false;

			if (self.Location != dockTrait.DockLocation)
				return false;

			dockTrait.Unreserve(self);

			return true;
		}

		public void MoveInForDocking(Actor self, Actor host)
		{
			Dock = host;
			var dockTrait = host.Trait<AllowsDocking>();
			var dockSequence = dockTrait.DockingSequence(host, self);

			self.CancelActivity();
			self.QueueActivity(new MoveAdjacentTo(self, Target.FromActor(host)));
			self.QueueActivity(move.MoveTo(dockTrait.DockLocation, host));
			self.QueueActivity(dockSequence);
		}

		public virtual void OnDocking()
		{
		}
		
		public virtual void OnDocked()
		{
			if (Docked)
				return;

			Docked = true;
		}

		public virtual void OnUndocking()
		{
			Docked = false;
		}

		public virtual void OnUndocked()
		{
		}
	}
}
