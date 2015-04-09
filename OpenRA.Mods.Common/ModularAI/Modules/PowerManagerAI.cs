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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	[Desc("Logic module for ModularAI. Decides when more power-providing actors are needed.")]
	public class PowerManagerAIInfo : ITraitInfo, IAILogicInfo, Requires<ModularAIInfo>
	{
		[Desc("Name of the AI personality this module belongs to.")]
		public readonly string AIName;

		[Desc("Actor types names.")]
		public readonly string[] PowerProviders = { };

		[Desc("How close should the AI be to using all its power before deciding to get more.")]
		public readonly int DangerPowerLevel = 50;

		[Desc("Ticks in between  reevaluating decisions and updating orders. Defaults to 1 second.")]
		public readonly int TickInterval = 25;

		string IAILogicInfo.AIName { get { return AIName; } }

		public object Create(ActorInitializer init) { return new PowerManagerAI(init.Self, this); }
	}

	public class PowerManagerAI : IAILogic
	{
		public string AIName { get { return info.AIName; } }

		readonly ModularAI ai;
		readonly PowerManagerAIInfo info;
		readonly Lazy<PowerManager> powerManager;

		int ticksSinceLastScan;

		public PowerManagerAI(Actor self, PowerManagerAIInfo info)
		{
			this.info = info;
			powerManager = Exts.Lazy(self.TraitOrDefault<PowerManager>);
			ai = self.TraitsImplementing<ModularAI>().FirstOrDefault(x => x.Info.Name == AIName);
		}

		public void Tick(Actor self)
		{
			if (powerManager.Value == null || --ticksSinceLastScan > 0)
				return;

			ticksSinceLastScan = info.TickInterval;

			if (powerManager.Value.ExcessPower > info.DangerPowerLevel)
				return;

			ai.Debug("Power level dangerously low! - {0}", powerManager.Value.ExcessPower);

			ai.ProduceActorFromList(info.PowerProviders, true);
		}
	}
}