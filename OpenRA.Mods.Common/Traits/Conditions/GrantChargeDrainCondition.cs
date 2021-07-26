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
using OpenRA.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Grants a condition for a certain time when deployed. Can be reversed anytime " +
		"by undeploying. Leftover charges are taken into account when recharging.")]
	public class GrantChargeDrainConditionInfo : PausableConditionalTraitInfo
	{
		// [FieldLoader.Require]
		// [Desc("Amount of charge points the actor can build up to.")]
		// public readonly int MaxChargePoints = 0;
		[FieldLoader.Require]
		[Desc("The amount of time in ticks it takes to fully recharge.")]
		public readonly int ChargeTicks = 0;

		// [FieldLoader.Require]
		// [Desc("Time it takes to discharge one charge point.")]
		// public readonly int DrainDuration = 0;
		[FieldLoader.Require]
		[Desc("The amount of time in ticks it takes to fully discharge.")]
		public readonly int DischargeTicks = 0;

		[Desc("How charged does the actor spawn.")]
		public readonly int StartingChargeTicks = 0;

		[Desc("Minimum charge value required to activate the condition.")]
		public readonly int MinActivationChargeTicks = 0;

		[Desc("Allow the condition to be disabled mid-discharge.")]
		public readonly bool Interruptible = false;

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition granted after deploying.")]
		public readonly string DeployedCondition = null;

		[Desc("Cursor to display when able to (un)deploy the actor.")]
		public readonly string DeployCursor = "deploy";

		[Desc("Cursor to display when unable to (un)deploy the actor.")]
		public readonly string DeployBlockedCursor = "deploy-blocked";

		[VoiceReference]
		public readonly string Voice = "Action";

		public readonly bool ShowSelectionBar = true;
		public readonly Color ChargingColor = Color.DarkRed;
		public readonly Color DischargingColor = Color.DarkMagenta;

		public override object Create(ActorInitializer init) { return new GrantChargeDrainCondition(init.Self, this); }
	}

	public class GrantChargeDrainCondition : PausableConditionalTrait<GrantChargeDrainConditionInfo>,
		IResolveOrder, IIssueOrder, ISelectionBar, IOrderVoice, ISync, ITick, IIssueDeployOrder
	{
		enum TimedDeployState { Charging, Ready, Active, Deploying, Undeploying }

		readonly Actor self;

		int deployedToken = Actor.InvalidConditionToken;

		[Sync]
		int currentChargeTicks;

		// [Sync]
		// int ticks;
		TimedDeployState deployState;  // TODO: Can we sync enums yet?

		public GrantChargeDrainCondition(Actor self, GrantChargeDrainConditionInfo info)
			: base(info)
		{
			this.self = self;
		}

		protected override void Created(Actor self)
		{
			currentChargeTicks = Info.StartingChargeTicks;
			deployState = currentChargeTicks == Info.ChargeTicks ? TimedDeployState.Ready : TimedDeployState.Charging;

			// ticks = deployState == TimedDeployState.Charging ? Info.ChargeTicks : GetDischargeTicks();
			// ticks = deployState == TimedDeployState.Charging ? currentChargeTicks * Info.ChargeTicks / 100 : GetDischargeTicks();
			base.Created(self);
		}

		Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
		{
			return new Order("GrantChargeDrainCondition", self, queued);
		}

		bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued)
		{
			return CanDeploy();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				if (!IsTraitDisabled)
					yield return new DeployOrderTargeter("GrantChargeDrainCondition", 5,
						() => IsCursorBlocked() ? Info.DeployBlockedCursor : Info.DeployCursor);
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == "GrantChargeDrainCondition" ? new Order(order.OrderID, self, queued) : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "GrantChargeDrainCondition" || !CanDeploy())
				return;

			if (!order.Queued)
				self.CancelActivity();

			if (deployState != TimedDeployState.Active)
				self.QueueActivity(new CallFunc(Deploy));
			else
				self.QueueActivity(new CallFunc(RevokeDeploy));
		}

		bool IsCursorBlocked()
		{
			return !CanDeploy();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "GrantChargeDrainCondition" && CanDeploy() ? Info.Voice : null;
		}

		bool CanDeploy()
		{
			if (IsTraitPaused || IsTraitDisabled)
				return false;

			if (deployState == TimedDeployState.Charging && currentChargeTicks < Info.MinActivationChargeTicks)
				return false;

			if (deployState == TimedDeployState.Active && !Info.Interruptible)
				return false;

			if (deployState == TimedDeployState.Deploying || deployState == TimedDeployState.Undeploying)
				return false;

			return true;
		}

		void Deploy()
		{
			deployState = TimedDeployState.Deploying;

			OnDeployCompleted();
		}

		void OnDeployCompleted()
		{
			if (deployedToken == Actor.InvalidConditionToken)
				deployedToken = self.GrantCondition(Info.DeployedCondition);

			// currentChargeTicks = Info.DischargeTicks;
			currentChargeTicks = Info.DischargeTicks * currentChargeTicks / Info.ChargeTicks;
			deployState = TimedDeployState.Active;
		}

		void RevokeDeploy()
		{
			deployState = TimedDeployState.Undeploying;

			OnUndeployCompleted();
		}

		void OnUndeployCompleted()
		{
			if (deployedToken != Actor.InvalidConditionToken)
				deployedToken = self.RevokeCondition(deployedToken);

			currentChargeTicks = Info.ChargeTicks * currentChargeTicks / Info.DischargeTicks;
			deployState = TimedDeployState.Charging;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitPaused || IsTraitDisabled)
				return;

			if (deployState == TimedDeployState.Ready || deployState == TimedDeployState.Deploying || deployState == TimedDeployState.Undeploying)
				return;

			// if (--ticks != 0)
			// 	return;
			//
			// if (deployState == TimedDeployState.Charging)
			// {
			// 	currentChargeTicks++;
			// 	if (currentChargeTicks == 100)
			// 	{
			// 		deployState = TimedDeployState.Ready;
			// 		ticks = GetDischargeTicks();
			// 	}
			// 	else
			// 		ticks = Info.ChargeTicks;
			// }
			// else
			// {
			// 	currentChargeTicks = currentChargeTicks - (100 / Info.DischargeTicks);
			// 	if (currentChargeTicks == 0)
			// 	{
			// 		RevokeDeploy();
			// 		ticks = Info.ChargeTicks;
			// 	}
			// 	else
			// 		ticks = GetDischargeTicks();
			// }
			// // ticks++;
			// // currentChargeTicks = 100 * ticks / Info.ChargeTicks;
			if (deployState == TimedDeployState.Charging)
			{
				currentChargeTicks++;
				if (currentChargeTicks == Info.ChargeTicks)
					deployState = TimedDeployState.Ready;
			}
			else
			{
				currentChargeTicks--;
				if (currentChargeTicks == 0)
				{
					RevokeDeploy();
				}
			}
		}

		float ISelectionBar.GetValue()
		{
			if (IsTraitDisabled || !Info.ShowSelectionBar)
				return 0f;

			return deployState == TimedDeployState.Active
				? (float)currentChargeTicks / Info.DischargeTicks
				: (float)currentChargeTicks / Info.ChargeTicks;
		}

		bool ISelectionBar.DisplayWhenEmpty => !IsTraitDisabled && Info.ShowSelectionBar;

		Color ISelectionBar.GetColor() => deployState == TimedDeployState.Charging ? Info.ChargingColor : Info.DischargingColor;

		// int GetDischargeTicks() => Info.ChargeTicks * 100 / Info.DischargeTicks;
	}
}
