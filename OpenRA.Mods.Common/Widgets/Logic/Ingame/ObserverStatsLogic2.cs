#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ObserverStatsLogic2
	{
		//ContainerWidget basicStatsHeaders;
		//ContainerWidget economyStatsHeaders;
		//ContainerWidget productionStatsHeaders;
		//ContainerWidget combatStatsHeaders;
		//ContainerWidget earnedThisMinuteGraphHeaders;
		ScrollPanelWidget playerStatsPanel;
		//ScrollItemWidget basicPlayerTemplate;
		//ScrollItemWidget economyPlayerTemplate;
		ScrollItemWidget productionPlayerTemplate;
		//ScrollItemWidget combatPlayerTemplate;
		//ContainerWidget earnedThisMinuteGraphTemplate;
		ScrollItemWidget teamTemplate;
		//DropDownButtonWidget statsDropDown;
		IEnumerable<Player> players;
		World world;
		WorldRenderer worldRenderer;

		[ObjectCreator.UseCtor]
		public ObserverStatsLogic2(World world, WorldRenderer worldRenderer, Widget widget)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			players = world.Players.Where(p => !p.NonCombatant);

			playerStatsPanel = widget.Get<ScrollPanelWidget>("PLAYER_STATS_PANEL");
			playerStatsPanel.Layout = new GridLayout(playerStatsPanel);

			productionPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("PRODUCTION_PLAYER_TEMPLATE");

			teamTemplate = playerStatsPanel.Get<ScrollItemWidget>("TEAM_TEMPLATE");

			DisplayStats(ProductionStats);
		}

		void DisplayStats(Func<Player, ScrollItemWidget> createItem)
		{
			var teams = players.GroupBy(p => (world.LobbyInfo.ClientWithIndex(p.ClientIndex) ?? new Session.Client()).Team).OrderBy(g => g.Key);
			foreach (var t in teams)
			{
				var team = t;
				var tt = ScrollItemWidget.Setup(teamTemplate, () => false, () => { });
				tt.IgnoreMouseOver = true;
				tt.Get<LabelWidget>("TEAM").GetText = () => team.Key == 0 ? "No Team" : "Team " + team.Key;
				playerStatsPanel.AddChild(tt);
				foreach (var p in team)
				{
					var player = p;
					playerStatsPanel.AddChild(createItem(player));
				}
			}
		}

		ScrollItemWidget ProductionStats(Player player)
		{
			var template = SetupPlayerScrollItemWidget(productionPlayerTemplate, player);

			LobbyUtils.AddPlayerFlagAndName(template, player);

			template.Get<ObserverProductionIconsWidget>("PRODUCTION_ICONS").GetPlayer = () => player;
			template.Get<ObserverSupportPowerIconsWidget>("SUPPORT_POWER_ICONS").GetPlayer = () => player;

			return template;
		}

		ScrollItemWidget SetupPlayerScrollItemWidget(ScrollItemWidget template, Player player)
		{
			return ScrollItemWidget.Setup(template, () => false, () =>
			{
				var playerBase = world.Actors.FirstOrDefault(a => !a.IsDead && a.Info.HasTraitInfo<BaseBuildingInfo>() && a.Owner == player);
				if (playerBase != null)
					worldRenderer.Viewport.Center(playerBase.CenterPosition);
			});
		}

		static Color GetPowerColor(PowerState state)
		{
			if (state == PowerState.Critical) return Color.Red;
			if (state == PowerState.Low) return Color.Orange;
			return Color.LimeGreen;
		}

		class StatsDropDownOption
		{
			public string Title;
			public Func<bool> IsSelected;
			public Action OnClick;
		}
	}
}
