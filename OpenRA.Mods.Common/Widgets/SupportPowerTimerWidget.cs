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
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class SupportPowerTimerWidget : Widget
	{
		public readonly string Font = "Bold";
		readonly World world;
		readonly Dictionary<Player, SupportPowerManager> init;
		[Translate] public readonly string NoTeamLabel = "No Team";
		[Translate] public readonly string TeamLabel = "Team {0}";
		readonly int yIncrement;
		readonly int xIncrement;
		readonly int maxPlayerNameWidth;
		readonly SpriteFont font;
		readonly IEnumerable<IGrouping<int, KeyValuePair<Player, SupportPowerManager>>> playersByTeam;
		
		int maxPowerNameWidth;
		float2 offset;

		[ObjectCreator.UseCtor]
		public SupportPowerTimerWidget(World world)
		{
			init = new Dictionary<Player, SupportPowerManager>();
			foreach (var tp in world.ActorsWithTrait<SupportPowerManager>()
				.Where(p => !p.Actor.IsDead && !p.Actor.Owner.NonCombatant))
				init[tp.Actor.Owner] = tp.Trait;

			font = Game.Renderer.Fonts[Font];
			this.world = world;
			yIncrement = font.Measure(" ").Y + 5;
			xIncrement = font.Measure("   ").X;
			maxPlayerNameWidth = init.Keys.Max(x => font.Measure(x.PlayerName).X);
			playersByTeam = init.GroupBy(x => world.LobbyInfo.ClientWithIndex(x.Key.ClientIndex).Team);
		}

		public override void Draw()
		{
			if (!IsVisible())
				return;

			offset = new float2(0, 0);
			var startPosition = new float2(Bounds.Location);
			maxPowerNameWidth = init.Values.Max(x => x.Powers.Max(y => font.Measure(y.Value.Info.Description).X));

			foreach (var team in playersByTeam)
			{
				// Draw team label
				var teamNumber = world.LobbyInfo.ClientWithIndex(team.First().Key.ClientIndex).Team;
				offset.X = 0;
				font.DrawTextWithContrast(TeamLabel.F(teamNumber), startPosition + offset, Color.White, Color.Black, 1);
				offset.X = xIncrement;
				offset.Y += yIncrement;

				foreach (var kvp in team)
				{
					// Draw player name
					var playerColor = kvp.Key.Color.RGB;
					font.DrawTextWithContrast(kvp.Key.PlayerName, startPosition + offset, playerColor, Color.Black, 1);

					// Draw support power names and timers
					foreach (var supportPowerInstance in kvp.Value.Powers)
					{
						var powerName = supportPowerInstance.Value.Info.Description;
						var remainingTime = WidgetUtils.FormatTime(supportPowerInstance.Value.RemainingTime, false);

						var nameOffset = startPosition.X + offset.X + maxPlayerNameWidth + xIncrement;
						var timeOffset = nameOffset + maxPowerNameWidth + xIncrement;
						var yOffset = startPosition.Y + offset.Y;

						font.DrawTextWithContrast(powerName, new float2(nameOffset, yOffset), playerColor, Color.Black, 1);

						//offset.X += middleColumnWidth + xIncrement;
						font.DrawTextWithContrast(remainingTime, new float2(timeOffset, yOffset), playerColor, Color.Black, 1);

						offset.Y += yIncrement;
					}
				}
			}
		}
	}
}
