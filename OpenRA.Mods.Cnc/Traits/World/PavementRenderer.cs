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
			borderIndices = BorderIndicesPerTilesetId[templatedTerrainInfo.Id];

			pavementLayer.Occupied.CellEntryChanged += Add;
		}

		[Flags]
		enum Adjacency : byte
		{
			None = 0x0,

			/// <summary> Depending on the map grid type: Rectangular - Bottom; Isometric - BottomLeft. </summary>
			PlusY = 0x1,

			/// <summary> Depending on the map grid type: Rectangular - Left; Isometric - TopLeft. </summary>
			MinusX = 0x2,

			/// <summary> Depending on the map grid type: Rectangular - Top; Isometric - TopRight. </summary>
			MinusY = 0x4,

			/// <summary> Depending on the map grid type: Rectangular - Right; Isometric - BottomRight. </summary>
			PlusX = 0x8,
		}

		IReadOnlyDictionary<Adjacency, ushort> borderIndices;

		// 596(TEMPERATE)/1046(SNOW) is unused (pavement sides with a clear hole in the middle).
		static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<Adjacency, ushort>> BorderIndicesPerTilesetId = new Dictionary<string, IReadOnlyDictionary<Adjacency, ushort>>
		{
			{
				"TEMPERATE",
				new Dictionary<Adjacency, ushort>
				{
					{ Adjacency.None, 671 },
					{ Adjacency.MinusY, 597 },
					{ Adjacency.PlusX, 598 },
					{ Adjacency.MinusY | Adjacency.PlusX, 599 },
					{ Adjacency.PlusY, 600 },
					{ Adjacency.PlusY | Adjacency.MinusY, 601 },
					{ Adjacency.PlusY | Adjacency.PlusX, 602 },
					{ Adjacency.PlusY | Adjacency.MinusY | Adjacency.PlusX, 603 },
					{ Adjacency.MinusX, 604 },
					{ Adjacency.MinusX | Adjacency.MinusY, 605 },
					{ Adjacency.MinusX | Adjacency.PlusX, 606 },
					{ Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusX, 607 },
					{ Adjacency.MinusX | Adjacency.PlusY, 608 },
					{ Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusY, 609 },
					{ Adjacency.MinusX | Adjacency.PlusY | Adjacency.PlusX, 609 },
					{ Adjacency.PlusY | Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusX, 611 },
				}
			},
			{
				"SNOW",
				new Dictionary<Adjacency, ushort>
				{
					{ Adjacency.None, 1031 },
					{ Adjacency.MinusY, 1047 },
					{ Adjacency.PlusX, 1048 },
					{ Adjacency.MinusY | Adjacency.PlusX, 1049 },
					{ Adjacency.PlusY, 1050 },
					{ Adjacency.PlusY | Adjacency.MinusY, 1051 },
					{ Adjacency.PlusY | Adjacency.PlusX, 1052 },
					{ Adjacency.PlusY | Adjacency.MinusY | Adjacency.PlusX, 1053 },
					{ Adjacency.MinusX, 1054 },
					{ Adjacency.MinusX | Adjacency.MinusY, 1055 },
					{ Adjacency.MinusX | Adjacency.PlusX, 1056 },
					{ Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusX, 1057 },
					{ Adjacency.MinusX | Adjacency.PlusY, 1058 },
					{ Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusY, 1059 },
					{ Adjacency.MinusX | Adjacency.PlusY | Adjacency.PlusX, 1059 },
					{ Adjacency.PlusY | Adjacency.MinusX | Adjacency.MinusY | Adjacency.PlusX, 1061 },
				}
			},
		};

		Adjacency FindClearSides(CPos cell)
		{
			// Borders are only valid on flat cells
			if (world.Map.Ramp[cell] != 0)
				return Adjacency.None;

			var clearSides = Adjacency.None;
			if (!pavementLayer.Occupied[cell + new CVec(0, -1)])
				clearSides |= Adjacency.MinusY;

			if (!pavementLayer.Occupied[cell + new CVec(-1, 0)])
				clearSides |= Adjacency.MinusX;

			if (!pavementLayer.Occupied[cell + new CVec(1, 0)])
				clearSides |= Adjacency.PlusX;

			if (!pavementLayer.Occupied[cell + new CVec(0, 1)])
				clearSides |= Adjacency.PlusY;

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

			var clearSides = FindClearSides(cell);
			borderIndices.TryGetValue(clearSides, out var tileTemplateId);

			var template = templatedTerrainInfo.Templates[tileTemplateId];
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
