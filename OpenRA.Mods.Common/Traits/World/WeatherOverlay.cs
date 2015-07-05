using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("no Desc yet, sry")]
	public class WeatherOverlayInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new WeatherOverlay(init.World, this); }
	}

	public class WeatherOverlay : IRenderWeatherOverlay
	{
		struct WeatherTile //Snowflake //raindrops
		{
			public readonly float2 ScreenPosition;
			public readonly byte Variant;

			public Sprite Snowflake;
			public Sprite Raindrop;

			public WeatherTile(float2 screenPosition, byte variant)
			{
				ScreenPosition = screenPosition;
				Variant = variant;

				Snowflake = null;
				Raindrop = null;
			}
		}
		readonly WeatherOverlayInfo info;
		readonly Sprite[] SnowflakeSprites, RaindropSprites;
		readonly CellLayer<WeatherTile> tiles;
		readonly Map map;

		PaletteReference SnowflakePalette, RaindropPalette;

		//Constructor
		public WeatherOverlay(World world, WeatherOverlayInfo info)
		{
			//     this.info = info;
			//      map = world.Map;

			// Load sprite variants
		}

		//Update
		public void UpdateWeatherOverlay()
		{

		}


		//Draw
		public void RenderWeatherOverlay(WorldRenderer wr)
		{
			UpdateWeatherOverlay();

			var posX = Game.Settings.Graphics.WindowedSize.X;
			var posY = Game.Settings.Graphics.WindowedSize.Y;

			//Draw something
			// Game.Renderer.WorldSpriteRenderer.DrawSprite(SnowflakeSprites[0], new float2(posX / 2, posY / 2), SnowflakePalette);

			//a line
			//  float2 left = new float2(0,0);
			//  float2 top = new float2(200,200);
			//   Game.Renderer.WorldLineRenderer.DrawLine(left, top, Color.Pink, Color.Pink);

		}


	}
}