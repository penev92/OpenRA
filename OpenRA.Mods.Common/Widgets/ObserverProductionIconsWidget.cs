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
		public readonly string TooltipTemplate = "PRODUCTION_TOOLTIP";	// This is missing from YAML?!?
		public readonly string TooltipContainer;
		public Func<Player> GetPlayer;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly int timestep;

		public int IconWidth = 32;//(int)(64 * 0.75f);
		public int IconHeight = 24;//(int)(48 * 0.75f);
		public int IconSpacing = 1;
		public int IconVerticalOffset = 1;
		public float IconScale = 0.75f;		// SHPs are 64x48, so scale down.

		public string ClockAnimation = "clock";
		public string ClockSequence = "idle";
		public string ClockPalette = "chrome";

		public ProductionIcon TooltipIcon { get; private set; }
		public Func<ProductionIcon> GetTooltipIcon;

		readonly Dictionary<ProductionQueue, Animation> clocks;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly List<ProductionIcon> productionIcons = new List<ProductionIcon>();
		readonly List<Rectangle> productionIconsBounds = new List<Rectangle>();
		float2 iconSize;
		int lastIconIdx = 0;

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
		}

		public override void Draw()
		{
			productionIcons.Clear();
			productionIconsBounds.Clear();

			var player = GetPlayer();
			if (player == null)
				return;

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(a => a.Actor.Owner == player)
				.Select((a, i) => new { a.Trait, i });

			foreach (var queue in queues)
				if (!clocks.ContainsKey(queue.Trait))
					clocks.Add(queue.Trait, new Animation(world, ClockAnimation));

			var currentItemsByItem = queues     // TODO: Change to grouping by actor type.
				.Select(a => a.Trait.CurrentItem())
				.Where(pi => pi != null)
				.GroupBy(pr => pr.Item)
				.OrderBy(g => g.First().Queue.Info.Type)
				.ThenBy(g => g.First().Item)
				.ToList();

			Bounds.Width = currentItemsByItem.Count * (IconWidth + IconSpacing);

			var queueCol = 0;
			foreach (var currentItems in currentItemsByItem)
			{
				var current = currentItems.OrderBy(pi => pi.Done ? 0 : (pi.Paused ? 2 : 1)).ThenBy(q => q.RemainingTimeActual).First();
				var queue = current.Queue;

				var faction = queue.Actor.Owner.Faction.InternalName;
				var actor = queue.AllItems().FirstOrDefault(a => a.Name == current.Item);
				if (actor == null)
					continue;

				var rsi = actor.TraitInfo<RenderSpritesInfo>();
				var icon = new Animation(world, rsi.GetImage(actor, world.Map.Rules.Sequences, faction));
				var bi = actor.TraitInfo<BuildableInfo>();
				icon.Play(bi.SmallIcon);

				var topLeftOffset = new float2(queueCol * (IconWidth + IconSpacing), 2);//IconVerticalOffset);	// HACK: This should be 1px because the parent
				// ScrollItemWidget is 26px high, the icon is 24px high and we want 1px spacing above and below the icon, but something is rendered 1px too low,
				// so we account for that manually by hacking a 2px offset here instead of 1px. It's wrong but looks proper.

				//if (loc.X + iconSize.X * 2 > Bounds.Width + 8)
				//	break;

				var iconTopLeft = RenderOrigin + topLeftOffset;
				var centerPosition = iconTopLeft + iconSize / 2;
				WidgetUtils.DrawSHPCentered(icon.Image, centerPosition, worldRenderer.Palette(bi.IconPalette), 1);//IconScale);

				productionIcons.Add(new ProductionIcon { Actor = actor, ProductionQueue = current.Queue });
				productionIconsBounds.Add(new Rectangle(new Point((int)iconTopLeft.X, (int)iconTopLeft.Y), new Size((int)iconSize.X, (int)iconSize.Y)));

				var pio = queue.Actor.Owner.PlayerActor.TraitsImplementing<IProductionIconOverlay>()
					.FirstOrDefault(p => p.IsOverlayActive(actor));

				if (pio != null)
					WidgetUtils.DrawSHPCentered(pio.Sprite, centerPosition + pio.Offset(iconSize),
						worldRenderer.Palette(pio.Palette), 1f);

				var clock = clocks[queue];
				clock.PlayFetchIndex(ClockSequence, () => current.TotalTime == 0 ? 0 :
					(current.TotalTime - current.RemainingTime) * (clock.CurrentSequence.Length - 1) / current.TotalTime);

				clock.Tick();
				WidgetUtils.DrawSHPCentered(clock.Image, centerPosition, worldRenderer.Palette(ClockPalette), 0.5f);//IconScale);

				var tiny = Game.Renderer.Fonts["Tiny"];
				var text = GetOverlayForItem(current, timestep);
				//tiny.DrawTextWithContrast(text,
				//	centerPosition - new float2(tiny.Measure(text).X / 2, 3) + new float2(0, 10),
				//	Color.White, Color.Black, 1);

				if (currentItems.Count() > 1)
				{
					var bold = Game.Renderer.Fonts["Bold"];
					text = currentItems.Count().ToString();
					bold.DrawTextWithContrast(text,
						new float2(RenderBounds.Location) + new float2(queueCol * (IconWidth + IconSpacing) + 5, 5),
						Color.White, Color.Black, 1);
				}

				queueCol++;
			}

			var parentWidth = Bounds.X + Bounds.Width;
			var gradientWidth = Math.Max(Bounds.Width, IconWidth + IconSpacing) + 20;

			Parent.Bounds.Width = parentWidth;
			var gradient = Parent.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");
			gradient.Bounds.Width = gradientWidth;

			var widestChildWidth = Parent.Parent.Children.Max(x => x.Bounds.Width);

			// Add 25 pixels to account for the scrollbar on the left and 25 for gradiend fade-out after the icons.
			Parent.Parent.Bounds.Width = Math.Max(25 + widestChildWidth, gradient.Bounds.Left + gradientWidth) + 25;
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
			if (TooltipContainer == null)
				return;

			for (var i = 0; i < productionIconsBounds.Count; i++)
			{
				if (!productionIconsBounds[i].Contains(Viewport.LastMousePos))
					continue;

				TooltipIcon = productionIcons[i];
				break;
			}

			tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "player", GetPlayer() }, { "getTooltipIcon", GetTooltipIcon } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null)
				return;

			tooltipContainer.Value.RemoveTooltip();
		}

		public override void Tick()
		{
			if (lastIconIdx >= productionIconsBounds.Count)
			{
				TooltipIcon = null;
				return;
			}

			if (TooltipIcon != null && productionIconsBounds[lastIconIdx].Contains(Viewport.LastMousePos))
				return;

			for (var i = 0; i < productionIconsBounds.Count; i++)
			{
				if (!productionIconsBounds[i].Contains(Viewport.LastMousePos))
					continue;

				lastIconIdx = i;
				TooltipIcon = productionIcons[i];
				return;
			}

			TooltipIcon = null;
		}
	}
}
