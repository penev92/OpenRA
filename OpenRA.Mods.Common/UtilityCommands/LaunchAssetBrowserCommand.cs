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
		const string ListPackages = nameof(ListPackages);
		const string GetSpriteFramesCount = nameof(GetSpriteFramesCount);
		const string LoadAsset = nameof(LoadAsset);
		const string SendingAsset = nameof(SendingAsset);
		const string UpdateState = nameof(UpdateState);

		const int Port = 6464;
		const int StreamingChunkSize = 204800;

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

		void SendMessage<T>(WebSocketSession session, string commandName, T payload)
		{
			var message = JsonSerializer.Serialize(new
			{
				CommandName = commandName,
				Data = payload
			});

			session.SendMessage(message);
		}

		void SendData(WebSocketSession session, byte[] data)
		{
			session.SendMessage(data, true);
		}

		void MessageHandler(WebSocketSession sender, string rawMessage)
		{
			var message = JsonSerializer.Deserialize<Dictionary<string, string>>(rawMessage);
			switch (message[CommandName])
			{
				case UpdateState:
					UpdateStateMessageHandler(sender, message);
					break;
				case GetSpriteFramesCount:
					GetSpriteFramesCountMessageHandler(sender, message["AssetName"]);
					break;
				case LoadAsset:
					LoadAssetMessageHandler(sender, message);
					break;
			}
		}

		void UpdateStateMessageHandler(WebSocketSession session, IDictionary<string, string> requestData)
		{
			if (requestData.ContainsKey("PaletteName"))
				assetBrowser.SetPalette(requestData["PaletteName"]);
		}

		void GetSpriteFramesCountMessageHandler(WebSocketSession session, string assetName)
		{
			var count = assetBrowser.GetSpriteFramesCount(assetName);
			SendMessage(session, GetSpriteFramesCount, count);
		}

		void LoadAssetMessageHandler(WebSocketSession session, IDictionary<string, string> requestData)
		{
			var assetName = requestData["AssetName"];
			var assetType = assetBrowser.GetAssetType(assetName);
			switch (assetType)
			{
				case AssetBrowserLogic.AssetType.Sprite:
					var spriteData = assetBrowser.LoadSpriteAsset(assetName, requestData);
					if (spriteData != null)
					{
						SendMessage(session, SendingAsset, new { AssetType = assetType.ToString() });
						SendData(session, spriteData);
					}

					break;
				case AssetBrowserLogic.AssetType.Model:
					SendMessage(session, Chat, $"Not yet supported asset type for {assetName}!");
					break;
				case AssetBrowserLogic.AssetType.Audio:
					var audioData = assetBrowser.LoadAudioAsset(assetName);
					if (audioData != null)
					{
						SendMessage(session, SendingAsset, new { AssetType = assetType.ToString() });

						var remaining = audioData.Length;
						for (var i = 0; i < audioData.Length; i += StreamingChunkSize)
						{
							var chunkSize = Math.Min(remaining, StreamingChunkSize);

							var audioChunk = new byte[chunkSize];
							Buffer.BlockCopy(audioData, i, audioChunk, 0, chunkSize);
							remaining -= chunkSize;

							SendData(session, audioChunk);
						}
					}

					break;
				case AssetBrowserLogic.AssetType.Video:
					SendMessage(session, Chat, $"Not yet supported asset type for {assetName}!");
					break;
				case AssetBrowserLogic.AssetType.Unknown:
					SendMessage(session, Chat, $"Unable to load asset {assetName}!");
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}

	class AssetBrowserLogic
	{
		public enum AssetType { Unknown = 0, Sprite = 1, Model = 2, Audio = 3, Video = 4 }

		readonly ModData modData;
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
		}

		public AssetType GetAssetType(string fileName)
		{
			var fileExtension = Path.GetExtension(fileName.ToLowerInvariant());
			if (allowedSpriteExtensions.Contains(fileExtension))
				return AssetType.Sprite;

			if (allowedModelExtensions.Contains(fileExtension))
				return AssetType.Model;

			if (allowedAudioExtensions.Contains(fileExtension))
				return AssetType.Audio;

			if (allowedVideoExtensions.Contains(fileExtension))
				return AssetType.Video;

			return AssetType.Unknown;
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
			if (GetAssetType(fileName) == AssetType.Sprite && TryLoadAsset(fileName, out var stream))
				return FrameLoader.GetFrames(stream, modData.SpriteLoaders, out _).Length;

			return 0;
		}

		public byte[] LoadSpriteAsset(string assetFileName, IDictionary<string, string> requestData)
		{
			if (!TryLoadAsset(assetFileName, out var stream))
				return null;

			var frameNumber = int.Parse(requestData["FrameNumber"]);
			var frames = FrameLoader.GetFrames(stream, modData.SpriteLoaders, out _);
			if (frameNumber < 0 || frameNumber >= frames.Length)
				return null;

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
			var bytes = png.Save();
			stream?.Dispose();
			return bytes;
		}

		public byte[] LoadAudioAsset(string fileName)
		{
			using (var soundStream = modData.DefaultFileSystem.Open(fileName))
				foreach (var modDataSoundLoader in modData.SoundLoaders)
					if (modDataSoundLoader.TryParseSound(soundStream, out var soundFormat))
						using (var pcmStream = soundFormat.GetPCMInputStream())
						{
							// Convert the PCM data we have to "non-interleaved IEEE754 32-bit linear PCM with a nominal range between -1 and +1"
							// As described here https://developer.mozilla.org/en-US/docs/Web/API/AudioBuffer
							var bytes = pcmStream.ReadAllBytes();
							var floats = new float[bytes.Length];
							for (var i = 0; i < bytes.Length; i += 2)
							{
								var localBytes = new[] { bytes[i], bytes[i + 1] };
								var value = BitConverter.ToInt16(localBytes, 0);
								var newFloat = (float)value / short.MaxValue;

								// Add the value to the results once for single channel audio.
								floats[i] = newFloat;

								// Add the value to the results a second time for dual-channel audio.
								floats[i + 1] = newFloat;
							}

							// Once the data is in the correct format, convert to a byte array for faster/easier transportation.
							var byteArray = new byte[floats.Length * 4];
							Buffer.BlockCopy(floats, 0, byteArray, 0, byteArray.Length);

							return byteArray;
						}

			return null;
		}

		#region Private methods

		bool TryLoadAsset(string fileName, out Stream stream)
		{
			return modData.DefaultFileSystem.TryOpen(fileName, out stream);
		}

		#endregion
	}
}
