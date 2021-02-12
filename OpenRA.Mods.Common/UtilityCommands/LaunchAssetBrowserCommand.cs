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
		const string GetSpriteFramesCount = nameof(GetSpriteFramesCount);
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
			// payload = System.IO.File.ReadAllBytes(@"D:\Work.Personal\DiscordBots\surprise-motherfucker.mp3");
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
					SetPaletteMessageHandler(message["PaletteName"]);
					break;
				case GetSpriteFramesCount:
					GetSpriteFramesCountMessageHandler(sender, message["AssetName"]);
					break;
				case LoadAsset:
					LoadAssetMessageHandler(sender, message);
					break;
				case "PlaySound":
					SendMessage(sender, "PlaySound", File.ReadAllBytes(@"D:\Work.Personal\DiscordBots\surprise-motherfucker.mp3"));
					break;
			}
		}

		void SetPaletteMessageHandler(string paletteName)
		{
			assetBrowser.SetPalette(paletteName);
		}

		void GetSpriteFramesCountMessageHandler(WebSocketSession session, string assetName)
		{
			var count = assetBrowser.GetSpriteFramesCount(assetName);
			SendMessage(session, GetSpriteFramesCount, count);
		}

		void LoadAssetMessageHandler(WebSocketSession session, IDictionary<string, string> request)
		{
			var assetName = request["AssetName"];
			var bytes = assetBrowser.LoadAsset(assetName, request);
			if (bytes != null)
			{
				// session.SendMessage(bytes, true);
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

			var stream = modData.DefaultFileSystem.Open(paletteFileName);

			var palette = new ImmutablePalette(stream, shadowIndex);
			currentPaletteColors = new Color[Palette.Size];
			for (var i = 0; i < Palette.Size; i++)
				currentPaletteColors[i] = palette.GetColor(i);
		}

		public int GetSpriteFramesCount(string fileName)
		{
			if (TryLoadAsset(fileName, out var stream, out var fileExtension) && allowedSpriteExtensions.Contains(fileExtension))
				return FrameLoader.GetFrames(stream, modData.SpriteLoaders, out _).Length;

			return 0;
		}

		public float[] LoadAsset(string assetFileName, IDictionary<string, string> request)
		{
			if (!TryLoadAsset(assetFileName, out var stream, out var fileExtension))
				return null;

			if (allowedSpriteExtensions.Contains(fileExtension))
				return null; // return LoadSpriteAsset(stream, request);

			if (allowedModelExtensions.Contains(fileExtension))
				return null; // TODO: Not implemented yet.

			if (allowedAudioExtensions.Contains(fileExtension))
				return LoadAudioAsset(assetFileName); // return stream.ReadAllBytes();

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

		byte[] LoadSpriteAsset(Stream stream, IDictionary<string, string> request)
		{
			var frameNumber = int.Parse(request["FrameNumber"]);
			var frames = FrameLoader.GetFrames(stream, modData.SpriteLoaders, out _);

			var usePadding = true; // TODO: Handle padding.

			var frame = frames[frameNumber];
			var frameSize = usePadding && !frame.DisableExportPadding ? frame.FrameSize : frame.Size;
			var offset = usePadding && !frame.DisableExportPadding ? (frame.Offset - 0.5f * new float2(frame.Size - frame.FrameSize)).ToInt2() : int2.Zero;

			// shp(ts) may define empty frames
			// TODO: This sounds very wrong. D2k R8 tileset files also have empty frames but those work fine.
			if (frameSize.Width == 0 && frameSize.Height == 0)
			{
				return new byte[0];
			}

			// TODO: expand frame with zero padding
			var pngData = frame.Data;
			if (frameSize != frame.Size)
			{
				pngData = new byte[Math.Max(frameSize.Width * frameSize.Height, frame.Size.Width * frame.Size.Height)];
				for (var j = 0; j < frame.Size.Height; j++)
					Buffer.BlockCopy(frame.Data, j * frame.Size.Width, pngData, (j + offset.Y) * frameSize.Width + offset.X, frame.Size.Width);
			}

			var png = new Png(pngData, SpriteFrameType.Indexed8, Math.Max(frameSize.Width, frame.Size.Width), Math.Max(frameSize.Height, frame.Size.Height), currentPaletteColors);
			return png.Save();
		}

		float[] LoadAudioAsset(string fileName)
		{
			using (var soundStream = modData.DefaultFileSystem.Open(fileName))
				foreach (var modDataSoundLoader in modData.SoundLoaders)
					if (modDataSoundLoader.TryParseSound(soundStream, out var soundFormat))
						using (var pcmStream = soundFormat.GetPCMInputStream())
						{
							var bytes = pcmStream.ReadAllBytes();
							var floats = new List<float>();
							for (var i = 0; i < bytes.Length; i += 2)
							{
								var localBytes = new[] { bytes[i], bytes[i + 1] };
								var value = BitConverter.ToInt16(localBytes, 0);
								var newFloat = (float)value / short.MaxValue;
								floats.Add(newFloat);
								floats.Add(newFloat);
							}

							return floats.Take(400000).ToArray();
						}

			return null;
		}

		#endregion
	}
}
