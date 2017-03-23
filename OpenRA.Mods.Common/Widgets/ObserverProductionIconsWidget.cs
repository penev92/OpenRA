#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class ObserverProductionIconsWidget : Widget
	{
		public readonly string TooltipTemplate = "PRODUCTION_TOOLTIP";
		public readonly string TooltipContainer;
		public Func<Player> GetPlayer;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly int timestep;

		public int IconWidth = 32;
		public int IconHeight = 24;
		public int IconSpacing = 5;

		public string ClockAnimation = "clock";
		public string ClockSequence = "idle";
		public string ClockPalette = "chrome";

		public ProductionIcon TooltipIcon { get; private set; }
		public Func<ProductionIcon> GetTooltipIcon;

		Dictionary<ProductionQueue, Animation> clocks;
		float2 iconSize;
		Rectangle[] iconRects = new Rectangle[0];
		ProductionIcon[] icons;
		Rectangle renderBounds;
		int lastIconIdx;
		Lazy<TooltipContainerWidget> tooltipContainer;

		[ObjectCreator.UseCtor]
		public ObserverProductionIconsWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			clocks = new Dictionary<ProductionQueue, Animation>();
			timestep = world.IsReplay ? world.WorldActor.Trait<MapOptions>().GameSpeed.Timestep : world.Timestep;
			GetTooltipIcon = () => TooltipIcon;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
			iconSize = new float2(IconWidth, IconHeight);
		}

		protected ObserverProductionIconsWidget(ObserverProductionIconsWidget other)
			: base(other)
		{
			GetPlayer = other.GetPlayer;
			world = other.world;
			worldRenderer = other.worldRenderer;
			timestep = other.timestep;
			clocks = other.clocks;

			IconWidth = other.IconWidth;
			IconHeight = other.IconHeight;
			IconSpacing = other.IconSpacing;
			iconSize = new float2(IconWidth, IconHeight);

			ClockAnimation = other.ClockAnimation;
			ClockSequence = other.ClockSequence;
			ClockPalette = other.ClockPalette;

			TooltipIcon = other.TooltipIcon;
			GetTooltipIcon = () => TooltipIcon;

			TooltipTemplate = other.TooltipTemplate;
			TooltipContainer = other.TooltipContainer;

			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
			renderBounds = Rectangle.Empty;
		}

		public override void Draw()
		{
			var player = GetPlayer();
			if (player == null)
				return;

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(a => a.Actor.Owner == player)
				.Select((a, i) => new { a.Trait, i });

			foreach (var queue in queues)
				if (!clocks.ContainsKey(queue.Trait))
					clocks.Add(queue.Trait, new Animation(world, ClockAnimation));

			if (renderBounds != RenderBounds)
			{
				renderBounds = RenderBounds;
				InitIcons(renderBounds);
			}
			else
				for (var i = 0; i < icons.Length; i++)
					icons[i].Actor = null;

			var queueCol = -1;
			var currentItemsByItem = queues		// TODO: Change to grouping by actor type.
				.Select(a => a.Trait.CurrentItem())
				.Where(pi => pi != null)
				.GroupBy(pr => pr.Item)
				.OrderBy(g => g.First().Queue.Info.Type)
				.ThenBy(g => g.First().Item)
				.ToList();

			foreach (var currentItems in currentItemsByItem)
			{
				var current = currentItems.OrderBy(pi => pi.Done ? 0 : (pi.Paused ? 2 : 1)).ThenBy(q => q.RemainingTimeActual).First();
				var queue = current.Queue;

				var faction = queue.Actor.Owner.Faction.InternalName;
				var actor = queue.AllItems().FirstOrDefault(a => a.Name == current.Item);
				if (actor == null)
					continue;

				queueCol += 1;
				var loc = new float2(queueCol * (IconWidth * 2 + IconSpacing), 3);
				if (loc.X + iconSize.X * 2 > Bounds.Width + 8)
					break;

				var rsi = actor.TraitInfo<RenderSpritesInfo>();
				var icon = new Animation(world, rsi.GetImage(actor, world.Map.Rules.Sequences, faction));
				var bi = actor.TraitInfo<BuildableInfo>();
				icon.Play(bi.Icon);

				var location = new float2(RenderBounds.Location) + loc;
				WidgetUtils.DrawSHPCentered(icon.Image, location + iconSize, worldRenderer.Palette(bi.IconPalette), 1f);

				var pio = queue.Actor.Owner.PlayerActor.TraitsImplementing<IProductionIconOverlay>()
					.FirstOrDefault(p => p.IsOverlayActive(actor));

				if (pio != null)
					WidgetUtils.DrawSHPCentered(pio.Sprite, location + iconSize + pio.Offset(iconSize),
						worldRenderer.Palette(pio.Palette), 1f);

				var clock = clocks[queue];
				clock.PlayFetchIndex(ClockSequence,
					() => current.TotalTime == 0 ? 0 : ((current.TotalTime - current.RemainingTime)
					* (clock.CurrentSequence.Length - 1) / current.TotalTime));
				clock.Tick();
				WidgetUtils.DrawSHPCentered(clock.Image, location + iconSize, worldRenderer.Palette(ClockPalette), 1f);

				var tiny = Game.Renderer.Fonts["Tiny"];
				var text = GetOverlayForItem(current, timestep);
				tiny.DrawTextWithContrast(text,
					location + iconSize - new float2(tiny.Measure(text).X / 2, 3),
					Color.White, Color.Black, 1);

				if (currentItems.Count() > 1)
				{
					var bold = Game.Renderer.Fonts["Bold"];
					text = currentItems.Count().ToString();
					bold.DrawTextWithContrast(text,
						new float2(RenderBounds.Location) + new float2(queueCol * (IconWidth * 2 + IconSpacing) + 5, 5),
						Color.White, Color.Black, 1);
				}
			}
		}

		static string GetOverlayForItem(ProductionItem item, int timestep)
		{
			if (item.Paused)
				return "ON HOLD";

			if (item.Done)
				return "READY";

			return WidgetUtils.FormatTime(item.RemainingTimeActual, timestep);
		}

		public override Widget Clone()
		{
			return new ObserverProductionIconsWidget(this);
		}

		public override void MouseEntered()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate,
					new WidgetArgs() { { "player", GetPlayer() }, { "getTooltipIcon", GetTooltipIcon } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null)
				return;

			tooltipContainer.Value.RemoveTooltip();
		}

		public override void Tick()
		{
			if (TooltipIcon != null && iconRects[lastIconIdx].Contains(Viewport.LastMousePos))
				return;

			for (var i = 0; i < iconRects.Length; i++)
			{
				if (iconRects[i].Contains(Viewport.LastMousePos))
				{
					lastIconIdx = i;
					TooltipIcon = icons[i];
					return;
				}
			}

			TooltipIcon = null;
		}

		void InitIcons(Rectangle renderBounds)
		{
			var iconWidthWithSpacing = IconWidth + IconSpacing;
			var numOfIcons = renderBounds.Width / iconWidthWithSpacing;
			iconRects = new Rectangle[numOfIcons];
			icons = new ProductionIcon[numOfIcons];

			for (var i = 0; i < numOfIcons; i++)
			{
				iconRects[i] = new Rectangle(renderBounds.X + i * iconWidthWithSpacing, renderBounds.Y, IconWidth, IconHeight);
				icons[i] = new ProductionIcon();
			}
		}
	}
}
