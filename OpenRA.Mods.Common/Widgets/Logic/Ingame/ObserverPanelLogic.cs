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
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ObserverPanelLogic
	{
		readonly Widget observerPanel;
		readonly ContainerWidget teamTemplate;
		readonly ContainerWidget playerTemplate;
		readonly ContainerWidget production;

		Dictionary<Player, int> playerOffset;
		IOrderedEnumerable<IGrouping<int, Player>> teams;

		[ObjectCreator.UseCtor]
		public ObserverPanelLogic(World world, WorldRenderer worldRenderer, Widget widget)
		{
			observerPanel = widget;
			teamTemplate = observerPanel.Get<ContainerWidget>("TEAM_TEMPLATE");
			playerTemplate = observerPanel.Get<ContainerWidget>("PLAYER_TEMPLATE");
			production = observerPanel.Get<ContainerWidget>("PRODUCTION");

			observerPanel.RemoveChildren();

			SetupPlayers(world);

			var dropdown = observerPanel.Parent.Get<DropDownButtonWidget>("OBSERVER_PANEL_DROPDOWN");
			dropdown.GetText = () => "None";
			dropdown.OnMouseDown = _ =>
			{
				var options = new List<ObserverDropDownOption>
				{
					new ObserverDropDownOption
					{
						Title = "None",
						IsSelected = () => true,//basicStatsHeaders.Visible,
						OnClick = () =>
						{
							//ClearStats();
							//statsDropDown.GetText = () => "None";
							//DisplayStats(BasicStats);
						}
					},
					//new ObserverDropDownOption
					//{
					//	Title = "Economy",
					//	//IsSelected = () => economyStatsHeaders.Visible,
					//	OnClick = () =>
					//	{
					//		ClearStats();
					//		statsDropDown.GetText = () => "Economy";
					//		DisplayStats(EconomyStats);
					//	}
					//},
					new ObserverDropDownOption
					{
						Title = "Production",
						IsSelected = () => false,//productionStatsHeaders.Visible,
						OnClick = () =>
						{
							//ClearStats();
							dropdown.GetText = () => "Production";
							DisplayProduction();
						}
					},
					//new ObserverDropDownOption
					//{
					//	Title = "Combat",
					//	IsSelected = () => combatStatsHeaders.Visible,
					//	OnClick = () =>
					//	{
					//		ClearStats();
					//		statsDropDown.GetText = () => "Combat";
					//		DisplayStats(CombatStats);
					//	}
					//},
					//new ObserverDropDownOption
					//{
					//	Title = "Earnings (graph)",
					//	IsSelected = () => earnedThisMinuteGraphHeaders.Visible,
					//	OnClick = () =>
					//	{
					//		ClearStats();
					//		statsDropDown.GetText = () => "Earnings (graph)";
					//		EarnedThisMinuteGraph();
					//	}
					//}
				};

				Func<ObserverDropDownOption, ScrollItemWidget, ScrollItemWidget> setupItem = (option, template) =>
				{
					//return new ScrollItemWidget(Game.ModData.DefaultRules);
					var item = ScrollItemWidget.Setup(template, option.IsSelected, option.OnClick);
					item.Get<LabelWidget>("LABEL").GetText = () => option.Title;
					return item;
				};

				dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 150, options, setupItem);
			};

			//DisplayProduction();
		}

		void SetupPlayers(World world)
		{
			playerOffset = new Dictionary<Player, int>();

			var players = world.Players.Where(p => !p.NonCombatant);
			teams = players.GroupBy(p => (world.LobbyInfo.ClientWithIndex(p.ClientIndex) ?? new Session.Client()).Team).OrderBy(g => g.Key);

			foreach (var team in teams)
			{
				var teamPanel = teamTemplate.Clone();

				var lastChild = observerPanel.Children.LastOrDefault();
				var top = lastChild == null ? 0 : lastChild.Bounds.Height + lastChild.Bounds.Top;

				var b = teamPanel.Bounds;
				teamPanel.Bounds = new Rectangle(b.X, top, b.Width, b.Height);
				teamPanel.Get<LabelWidget>("TEAM").GetText = () => team.Key == 0 ? "No Team" : "Team " + team.Key;

				observerPanel.AddChild(teamPanel);

				foreach (var player in team)
				{
					var playerPanel = playerTemplate.Clone();

					lastChild = observerPanel.Children.LastOrDefault();
					top = lastChild == null ? 0 : lastChild.Bounds.Height + lastChild.Bounds.Top;

					b = playerPanel.Bounds;
					playerPanel.Bounds = new Rectangle(b.X, top, b.Width, b.Height);
					playerPanel.Get<LabelWidget>("NAME").GetText = () => player.PlayerName;
					playerPanel.Get<LabelWidget>("NAME").GetColor = () => player.Color.RGB; // TODO: Needs contrast.
					playerPanel.Get<ImageWidget>("FLAG").GetImageName = () => player.Faction.InternalName;
					//playerPanel.Get<ObserverProductionIconsWidget>("PRODUCTION_ICONS").GetPlayer = () => player;

					observerPanel.AddChild(playerPanel);

					playerOffset.Add(player, top);
				}
			}
		}

		void DisplayProduction()
		{
			var productionTemplate = production.Get<ContainerWidget>("PRODUCTION_PLAYER_TEMPLATE");

			production.RemoveChild(productionTemplate);

			foreach (var team in teams)
			{
				foreach (var player in team)
				{
					var top = playerOffset[player];
					var productionIconsPanel = productionTemplate.Clone();
					var b = productionIconsPanel.Bounds;
					productionIconsPanel.Bounds = new Rectangle(b.X, top, b.Width, b.Height);

					productionIconsPanel.Get<ObserverProductionIconsWidget>("PRODUCTION_ICONS").GetPlayer = () => player;

					production.AddChild(productionIconsPanel);
				}
			}

			observerPanel.AddChild(production);
		}
	}

	class ObserverDropDownOption
	{
		public string Title;
		public Func<bool> IsSelected;
		public Action OnClick;
	}
}
