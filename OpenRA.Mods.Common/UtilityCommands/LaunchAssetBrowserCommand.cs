#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
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
using System.Linq;
using System.Text.Json;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.UtilityCommands
{
	class LaunchAssetBrowserCommand : IUtilityCommand
	{
		const string CommandName = nameof(CommandName);
		const string Chat = nameof(Chat);
		const string SetPalette = nameof(SetPalette);
		const string ListPackages = nameof(ListPackages);
		const string LoadAsset = nameof(LoadAsset);

		const int Port = 6464;

		WebSocketServer webSocketServer;
		AssetBrowserLogic assetBrowser;

		string IUtilityCommand.Name => "--asset-browser";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length == 1;
		}

		[Desc("Launches the Asset Browser.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			assetBrowser = new AssetBrowserLogic(utility.ModData);

			webSocketServer = new WebSocketServer();
			webSocketServer.OnClientConnected += (sender, session) =>
			{
				SendMessage(session, ListPackages, assetBrowser.ListPackages());
			};

			webSocketServer.OnMessageReceived += (sender, messageRaw) =>
			{
				MessageHandler(sender as WebSocketSession, messageRaw);
			};

			webSocketServer.Listen(Port);
		}

		void SendMessage(WebSocketSession session, string commandName, object payload)
		{
			var message = JsonSerializer.Serialize(new
			{
				CommandName = commandName,
				Data = payload
			});

			session.SendMessage(message);
		}

		void MessageHandler(WebSocketSession sender, string rawMessage)
		{
			var message = JsonSerializer.Deserialize<Dictionary<string, string>>(rawMessage);
			switch (message[CommandName])
			{
				case SetPalette:
					SetPaletteMessageHandler(message["Data"]);
					break;
				case LoadAsset:
					LoadAssetMessageHandler(sender, message["Data"]);
					break;
			}
		}

		void SetPaletteMessageHandler(string paletteName)
		{
			assetBrowser.SetPalette(paletteName);
		}

		void LoadAssetMessageHandler(WebSocketSession session, string assetName)
		{
			var bytes = assetBrowser.LoadAsset(assetName);
			if (bytes != null)
			{
				SendMessage(session, Chat, $"Sending you asset {assetName}...");
				SendMessage(session, LoadAsset, bytes);
			}
			else
			{
				SendMessage(session, Chat, $"Unable to load asset {assetName}!");
			}
		}
	}

	class AssetBrowserLogic
	{
		readonly ModData modData;
		readonly string[] allowedExtensions;
		readonly string[] allowedSpriteExtensions;
		readonly string[] allowedModelExtensions;
		readonly string[] allowedAudioExtensions;
		readonly string[] allowedVideoExtensions;

		Color[] currentPaletteColors;

		public AssetBrowserLogic(ModData modData)
		{
			this.modData = modData;

			var assetBrowserModData = modData.Manifest.Get<AssetBrowser>();
			allowedSpriteExtensions = assetBrowserModData.SpriteExtensions;
			allowedModelExtensions = assetBrowserModData.ModelExtensions;
			allowedAudioExtensions = assetBrowserModData.AudioExtensions;
			allowedVideoExtensions = assetBrowserModData.VideoExtensions;
			allowedExtensions = allowedSpriteExtensions
				.Union(allowedModelExtensions)
				.Union(allowedAudioExtensions)
				.Union(allowedVideoExtensions)
				.ToArray();
		}

		public IDictionary<string, IEnumerable<string>> ListPackages()
		{
			return modData.ModFiles.MountedPackages.ToDictionary(x => x.Name, y => y.Contents);
		}

		public void SetPalette(string paletteFileName)
		{
			var shadowIndex = new int[] { }; // TODO: Handle shadow indices.

			var stream = modData.ModFiles.Open(paletteFileName);

			var palette = new ImmutablePalette(stream, shadowIndex);
			currentPaletteColors = new Color[Palette.Size];
			for (var i = 0; i < Palette.Size; i++)
				currentPaletteColors[i] = palette.GetColor(i);
		}

		public IDictionary<string, byte[]> LoadAsset(string assetFileName)
		{
			if (!TryLoadAsset(assetFileName, out var stream, out var fileExtension))
				return null;

			if (allowedSpriteExtensions.Contains(fileExtension))
				return LoadSpriteAsset(stream);

			if (allowedModelExtensions.Contains(fileExtension))
				return null; // TODO: Not implemented yet.

			if (allowedAudioExtensions.Contains(fileExtension))
				return null; // TODO: Not implemented yet.

			if (allowedVideoExtensions.Contains(fileExtension))
				return null; // TODO: Not implemented yet.

			return null;
		}

		#region Private methods

		bool TryLoadAsset(string fileName, out Stream stream, out string fileExtension)
		{
			fileExtension = Path.GetExtension(fileName.ToLowerInvariant());
			return modData.DefaultFileSystem.TryOpen(fileName, out stream);
		}

		IDictionary<string, byte[]> LoadSpriteAsset(Stream stream)
		{
			var result = new Dictionary<string, byte[]>();
			var frames = FrameLoader.GetFrames(stream, modData.SpriteLoaders, out _);

			var count = 0;
			var usePadding = false; // TODO: Handle padding.

			for (var i = 0; i < frames.Length; i++)
			{
				var frame = frames[i];
				var frameSize = usePadding && !frame.DisableExportPadding ? frame.FrameSize : frame.Size;
				var offset = usePadding && !frame.DisableExportPadding ? (frame.Offset - 0.5f * new float2(frame.Size - frame.FrameSize)).ToInt2() : int2.Zero;

				// shp(ts) may define empty frames
				if (frameSize.Width == 0 && frameSize.Height == 0)
				{
					count++;
					continue;
				}

				// TODO: expand frame with zero padding
				var pngData = frame.Data;
				if (frameSize != frame.Size)
				{
					pngData = new byte[frameSize.Width * frameSize.Height];
					for (var j = 0; j < frame.Size.Height; j++)
						Buffer.BlockCopy(frame.Data, j * frame.Size.Width, pngData, (j + offset.Y) * frameSize.Width + offset.X, frame.Size.Width);
				}

				var png = new Png(pngData, SpriteFrameType.Indexed8, frameSize.Width, frameSize.Height, currentPaletteColors);
				result.Add(i.ToString(), png.Save());
			}

			return result;
		}

		#endregion
	}
}
