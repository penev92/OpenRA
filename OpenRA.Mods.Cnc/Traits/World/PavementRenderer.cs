#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Attach this to the world actor. Renders the state of " + nameof(PavementLayer))]
	public class PavementRendererInfo : TraitInfo
	{
		[Desc("Palette to render the layer sprites in.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public override object Create(ActorInitializer init) { return new PavementRenderer(init.Self, this); }
	}

	public class PavementRenderer : IRenderOverlay, IWorldLoaded, ITickRender, IRadarTerrainLayer, INotifyActorDisposing
	{
		readonly World world;
		readonly PavementRendererInfo info;
		readonly PavementLayer pavementLayer;
		readonly Dictionary<CPos, Sprite> dirty;
		readonly ITiledTerrainRenderer terrainRenderer;
		readonly CellLayer<(Color, Color)> radarColor;

		TerrainSpriteLayer terrainSpriteLayer;
		ITemplatedTerrainInfo templatedTerrainInfo;
		PaletteReference paletteReference;

		public PavementRenderer(Actor self, PavementRendererInfo info)
		{
			this.info = info;
			world = self.World;
			dirty = new Dictionary<CPos, Sprite>();
			terrainRenderer = self.Trait<ITiledTerrainRenderer>();
			pavementLayer = self.Trait<PavementLayer>();
			radarColor = new CellLayer<(Color, Color)>(world.Map);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			paletteReference = wr.Palette(info.Palette);

			terrainSpriteLayer = new TerrainSpriteLayer(w, wr, terrainRenderer.MissingTile, BlendMode.Alpha, wr.World.Type != WorldType.Editor);

			if (!(w.Map.Rules.TerrainInfo is ITemplatedTerrainInfo terrainInfo))
				throw new InvalidDataException(nameof(PavementRenderer) + " requires a template-based tileset.");

			templatedTerrainInfo = terrainInfo;

			pavementLayer.Occupied.CellEntryChanged += Add;
		}

		[Flags]
		enum Adjacency : byte
		{
			None = 0x0,
			Left = 0x1,
			Top = 0x2,
			Right = 0x4,
			Bottom = 0x8,

			All = 0xFF
		}

		static readonly Dictionary<Adjacency, ushort> SpriteMap = new Dictionary<Adjacency, ushort>()
		{
			{ Adjacency.None, 671 },
			{ Adjacency.Right, 597 },
			{ Adjacency.Bottom, 598 },
			{ Adjacency.Right | Adjacency.Bottom, 599 },
			{ Adjacency.Left, 600 },
			{ Adjacency.Left | Adjacency.Right, 601 },
			{ Adjacency.Left | Adjacency.Bottom, 602 },
			{ Adjacency.Left | Adjacency.Right | Adjacency.Bottom, 603 },
			{ Adjacency.Top, 604 },
			{ Adjacency.Top | Adjacency.Right, 605 },
			{ Adjacency.Top | Adjacency.Bottom, 606 },
			{ Adjacency.Top | Adjacency.Right | Adjacency.Bottom, 607 },
			{ Adjacency.Top | Adjacency.Left, 608 },
			{ Adjacency.Top | Adjacency.Right | Adjacency.Left, 609 },
			{ Adjacency.Top | Adjacency.Left | Adjacency.Bottom, 609 },
			{ Adjacency.All, 611 },
		};

		Adjacency FindClearSides(CPos cell)
		{
			var clearSides = Adjacency.None;
			if (!pavementLayer.Occupied[cell + new CVec(0, -1)])
				clearSides |= Adjacency.Right;

			if (!pavementLayer.Occupied[cell + new CVec(-1, 0)])
				clearSides |= Adjacency.Top;

			if (!pavementLayer.Occupied[cell + new CVec(1, 0)])
				clearSides |= Adjacency.Bottom;

			if (!pavementLayer.Occupied[cell + new CVec(0, 1)])
				clearSides |= Adjacency.Left;

			return clearSides;
		}

		void Add(CPos cell)
		{
			UpdateRenderedSprite(cell);
			foreach (var direction in CVec.Directions)
			{
				var neighbor = direction + cell;
				UpdateRenderedSprite(neighbor);
			}
		}

		void UpdateRenderedSprite(CPos cell)
		{
			if (!pavementLayer.Occupied[cell])
				return;

			var clear = FindClearSides(cell);
			SpriteMap.TryGetValue(clear, out var tile);

			var template = templatedTerrainInfo.Templates[tile];
			var index = Game.CosmeticRandom.Next(template.TilesCount);
			var terrainTile = new TerrainTile(template.Id, (byte)index);

			var sprite = terrainRenderer.TileSprite(terrainTile);
			var offset = new float3(0, 0, -6);
			dirty[cell] = new Sprite(sprite.Sheet, sprite.Bounds, 1, sprite.Offset + offset, sprite.Channel, sprite.BlendMode);

			var tileInfo = world.Map.Rules.TerrainInfo.GetTerrainInfo(terrainTile);
			radarColor[cell] = (tileInfo.GetColor(world.LocalRandom), tileInfo.GetColor(world.LocalRandom));
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			var remove = new List<CPos>();
			foreach (var kv in dirty)
			{
				var cell = kv.Key;
				if (!self.World.FogObscures(cell))
				{
					var sprite = kv.Value;
					terrainSpriteLayer.Update(cell, sprite, paletteReference);
					remove.Add(cell);
				}
			}

			foreach (var r in remove)
				dirty.Remove(r);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			terrainSpriteLayer.Draw(wr.Viewport);
		}

		event Action<CPos> IRadarTerrainLayer.CellEntryChanged
		{
			add => radarColor.CellEntryChanged += value;
			remove => radarColor.CellEntryChanged -= value;
		}

		bool IRadarTerrainLayer.TryGetTerrainColorPair(MPos uv, out (Color Left, Color Right) value)
		{
			value = default;

			if (world.Map.CustomTerrain[uv] == byte.MaxValue)
				return false;

			var cell = uv.ToCPos(world.Map);
			if (!pavementLayer.Occupied[cell])
				return false;

			value = radarColor[uv];
			return true;
		}

		bool disposed;
		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			pavementLayer.Occupied.CellEntryChanged -= Add;

			terrainSpriteLayer.Dispose();
			disposed = true;
		}
	}
}
