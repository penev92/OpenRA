﻿#region Copyright & License Information
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
using System.IO;
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.SpriteLoaders
{
	public class GenieSlpLoader : ISpriteLoader
	{
		public class GenieSlpHeader
		{
			public readonly string Version;
			public readonly int FrameCount;
			public readonly string Comment;

			public GenieSlpHeader(Stream stream)
			{
				Version = stream.ReadASCII(4);
				FrameCount = stream.ReadInt32();
				Comment = stream.ReadASCII(24);
			}
		}

		class GenieSlpFrame : ISpriteFrame
		{
			const byte DEFAULT_INDEX = 0;
			const byte PLAYER_INDEX = 1;

			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public GenieSlpFrame(Stream stream, GenieSlpFrameHeader header)
			{
				Size = FrameSize = new Size(header.Width, header.Height);
				Offset = float2.Zero;

				var skipLeft = new ushort[header.Height];
				var skipRight = new ushort[header.Height];
				for (var i = 0; i < header.Height; i++)
				{
					skipLeft[i] = stream.ReadUInt16();
					skipRight[i] = stream.ReadUInt16();
				}

				var frameData = new List<byte[]>();

				for (var y = 0; y < header.Height; y++)
				{
					stream.Position = header.RowCommandOffsets[y];
					frameData.Add(ReadRowCommands(stream, header, skipLeft[y], skipRight[y]));
				}

				Data = frameData.SelectMany(x => x).ToArray();
			}

			static byte[] ReadRowCommands(Stream stream, GenieSlpFrameHeader header, ushort skipLeft, ushort skipRight)
			{
				var rowData = new List<byte>();
				for (var x = 0; x < header.Width; x++)
					rowData.Add(DEFAULT_INDEX);

				if (skipLeft == 0x8000 || skipRight == 0x8000)
					return rowData.ToArray();

				int opcode;
				var rowPosX = skipLeft;

				do
				{
					var currByte = GetSingleByte(stream);

					opcode = currByte & 0x0f;

					switch (opcode)
					{
					case 0xf: // End of Line
						break;

					case 0x0:
					case 0x4:
					case 0x8:
					case 0xc: // Block Copy (short)
						for (uint i = 0, cmdLen = GetTop6Bits(currByte); i < cmdLen; i++)
							rowData[rowPosX++] = GetSingleByte(stream);
						break;

					case 0x01:
					case 0x05:
					case 0x09:
					case 0xd: // Skip Pixels (short)
						rowPosX += GetTop6Bits(currByte);
						break;

					case 0x02: // Block Copy (long)
						for (uint i = 0, cmdLen = GetTopNibblePlusNext(currByte, stream); i < cmdLen; i++)
							rowData[rowPosX++] = GetSingleByte(stream);
						break;

					case 0x03: // Skip Pixels (long)
						rowPosX += (ushort)GetTopNibblePlusNext(currByte, stream);
						break;

					case 0x06: // Copy & Transform
						for (uint i = 0, cmdLen = GetTopNibbleOrNext(currByte, stream); i < cmdLen; i++)
							rowData[rowPosX++] = GetRealPlayerColorIndex(GetSingleByte(stream));
						break;

					case 0x7: // Fill Block
						{
							var cmdLen = GetTopNibbleOrNext(currByte, stream);
							var fill = GetSingleByte(stream);
							for (var i = 0; i < cmdLen; i++)
								rowData[rowPosX++] = fill;
						}
						break;

					case 0xa: // Transform Block (player color fill block?)
						{
							var cmdLen = GetTopNibbleOrNext(currByte, stream);
							var fill = GetRealPlayerColorIndex(GetSingleByte(stream));
							for (var i = 0; i < cmdLen; i++)
								rowData[rowPosX++] = fill;
						}
						break;

					case 0xb: // Shadow Pixels
						for (uint i = 0, cmdLen = GetTopNibbleOrNext(currByte, stream); i < cmdLen; i++)
							rowData[rowPosX++] = 56;
						break;

					case 0xe: // Extended Commands
						var high = (currByte & 0xf0) >> 4;
						switch (high)
						{
						case 0x0e: // Draw following command if *not* mirrored on the X axis
						case 0x1e: // Draw following command if mirrored on the X axis
						case 0x2e: // Set color transform table to 'normal'
						case 0x3e: // Set color transform table to 'alt'
						case 0x4e: // Obstructed player color
						case 0x5e: // Obstructed player color 2 (?)
						case 0x6e: // If pixel obstructed draw black outline
						case 0x7e: // If pixel obstructed draw black outline
							break;
						}
						break;
					}
				} while (opcode != 15);

				return rowData.ToArray();
			}

			static byte GetSingleByte(Stream stream) { return stream.ReadBytes(1)[0]; }
			static byte GetTop6Bits(byte input) { return (byte)((input & 0xFC) >> 2); }

			static uint GetTopNibblePlusNext(byte input, Stream stream)
			{
				var current = (input & 0xf0) << 4;
				var next = GetSingleByte(stream);

				return (uint)(current + next);
			}

			static uint GetTopNibbleOrNext(byte input, Stream stream)
			{
				var length = (input & 0xf0) >> 4;

				return length == 0 ? GetSingleByte(stream) : (uint)length;
			}

			static byte GetRealPlayerColorIndex(byte input) { return (byte)(input + PLAYER_INDEX * 16); }
		}

		class GenieSlpFrameHeader
		{
			public readonly uint CommandTableOffset;
			public readonly uint OutlineDataOffset;
			public readonly int Width;
			public readonly int Height;
			public readonly int HotspotX;
			public readonly int HotspotY;
			public readonly ushort LeftSkip;
			public readonly ushort RightSkip;
			public readonly uint[] RowCommandOffsets;
			public readonly int PixelWidth;

			public GenieSlpFrameHeader(Stream stream)
			{
				CommandTableOffset = stream.ReadUInt32();
				OutlineDataOffset = stream.ReadUInt32();

				// Skip unused PaletteOffset and Properties (4 each)
				stream.Position += 8;

				Width = stream.ReadInt32();
				Height = stream.ReadInt32();
				HotspotX = stream.ReadInt32();
				HotspotY = stream.ReadInt32();

				var pos = stream.Position;
				stream.Position = OutlineDataOffset;
				LeftSkip = stream.ReadUInt16();
				RightSkip = stream.ReadUInt16();

				stream.Position = CommandTableOffset;
				RowCommandOffsets = new uint[Height];

				for (var i = 0; i < Height; i++)
					RowCommandOffsets[i] = stream.ReadUInt32();

				// Move back to the end of this header.
				stream.Position = pos;

				PixelWidth = Width - LeftSkip - RightSkip;
			}
		}

		static bool IsGenieSlp(Stream stream)
		{
			var pos = stream.Position;
			var test = stream.ReadASCII(32);
			stream.Position = pos;

			// SWGB -> 2.0( / 2.0N
			// CC   -> 2.0(
			// AoE  -> 2.0N
			if (!test.StartsWith("2.0N") && !test.StartsWith("2.0("))
				return false;

			if (!test.EndsWith("\0\0\0ArtDesk 1.00 SLP Writer\0") && !test.EndsWith("\0\0\0RGE RLE shape file\0\0\0\0\0\0"))
				return false;

			return true;
		}

		static GenieSlpFrame[] ParseFrames(Stream stream)
		{
			var pos = stream.Position;

			var header = new GenieSlpHeader(stream);

			var frameHeaders = new GenieSlpFrameHeader[header.FrameCount];
			for (var i = 0; i < header.FrameCount; i++)
				frameHeaders[i] = new GenieSlpFrameHeader(stream);

			var frames = new GenieSlpFrame[header.FrameCount];
			for (var i = 0; i < header.FrameCount; i++)
				frames[i] = new GenieSlpFrame(stream, frameHeaders[i]);

			stream.Position = pos;
			return frames;
		}

		public bool TryParseSprite(Stream stream, out ISpriteFrame[] frames)
		{
			if (!IsGenieSlp(stream))
			{
				frames = null;
				return false;
			}

			frames = ParseFrames(stream);
			return true;
		}
	}
}