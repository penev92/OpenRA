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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	public class ModularAIInfo : IBotInfo, ITraitInfo
	{
		[Desc("Display name for this AI.")]
		public readonly string Name = "ModularAI";

		string IBotInfo.Name { get { return Name; } }

		/* TODO: The AI should be a state-machine for;
		 * repairing
		 * power
		 * money
		 * base defense
		 * etc.
		 */

		public object Create(ActorInitializer init) { return new ModularAI(init.Self, this); }
	}

	public interface IAILogicInfo
	{
		string AIName { get; }
	}
		
	public interface IAILogic
	{
		string AIName { get; }
		void Tick(Actor self);
	}

	public class ModularAI : ITick, IBot, IWorldLoaded, INotifyKilled
	{
		public void Activate(Player p)
		{
			Player = p;
			BotEnabled = p.IsBot;
			modules = p.PlayerActor.TraitsImplementing<IAILogic>()
				.Where(t => t.AIName == Info.Name).Distinct().ToList();
		}

		public IBotInfo Info { get { return info; } }
		public Player Player { get; private set; }
		public List<Actor> OwnedActors { get; private set; }
		public IEnumerable<Actor> GetIdleActors()
		{
			return OwnedActors.Where(a =>
				!a.IsDead &&
				a.IsInWorld &&
				a.IsIdle);
		}

		readonly World world;
		readonly ModularAIInfo info;

		public bool BotEnabled;
		List<IAILogic> modules;

		public ModularAI(Actor self, ModularAIInfo info)
		{
			Player = self.Owner;
			BotEnabled = Player.IsBot;
			world = self.World;
			this.info = info;
			OwnedActors = new List<Actor>();
			modules = new List<IAILogic>();
		}

		public void Killed(Actor self, AttackInfo e)
		{
			RemoveActor(self);
		}

		public void AddActor(Actor a)
		{
			if (OwnedActors.Contains(a))
				return;

			if (!a.HasTrait<ManagedByAI>())
				throw new YamlException("ModularAI cannot manage actor type `{0}` who does not have ManagedByAI."
					.F(a.Info.Name));

			OwnedActors.Add(a);
		}

		public void RemoveActor(Actor a)
		{
			if (!OwnedActors.Contains(a))
				return;

			OwnedActors.Remove(a);
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			// After this assignment we should add/remove
			// via (Add/Remove)Actor from module logic.
			OwnedActors = world.Actors.Where(a =>
				a.Owner == Player &&
				a.HasTrait<IPositionable>() &&
				a.HasTrait<ManagedByAI>()
			).ToList();
		}

		public void Tick(Actor self)
		{
			if (!BotEnabled || Player.WinState == WinState.Lost)
				return;

			TickInner(self);
			foreach (var mod in modules)
				mod.Tick(self);
		}

		protected virtual void TickInner(Actor self)
		{
			// Nothing to do here, but subclasses should override
			// this method, as everything in Tick must happen before this
			// is called.
		}

		public void Notify<T>(Action<T> action) where T : class
		{
			if (!BotEnabled)
				return;

			var cast = this as T;
			if (cast != null)
				action(cast);

			foreach (var mod in modules)
			{
				var obj = mod as T;
				if (obj != null)
					action(obj);
			}
		}

		public void Debug(string message, params object[] fmt)
		{
			if (!BotEnabled)
				return;

			message = "{0}: {1}".F(OwnerString(Player), message.F(fmt));

			if (Game.Settings.Debug.BotDebug)
				Game.Debug(message);

			Console.WriteLine(message);
		}

		static string OwnerString(Actor a)
		{
			return OwnerString(a.Owner);
		}

		static string OwnerString(Player p)
		{
			var n = p.PlayerName;
			return p.IsBot ? n + "_" + p.ClientIndex.ToString()
					: n;
		}
	}
}