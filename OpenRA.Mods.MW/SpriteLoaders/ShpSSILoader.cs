#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Drawing;
using System.IO;
using OpenRA.Graphics;

namespace OpenRA.Mods.MW.SpriteLoaders
{
	public class ShpSSILoader : ISpriteLoader
	{
		bool isMechCommanderSSI = false;
		int additionalByteOffset;
		uint[] frameOffsets;

		bool IsShpSSI(Stream s)
		{
			var start = s.Position;
			isMechCommanderSSI = false;

			if (s.ReadASCII(4) != "1.10")
			{
				// MechCommander SHPs add 2 bytes of unknown purpose + a copy of frame count (4 bytes) before the string,
				// otherwise they're identical so we can ignore those 6 bytes
				s.Position = 6;
				if (s.ReadASCII(4) != "1.10")
				{
					s.Position = start;
					return false;
				}

				isMechCommanderSSI = true;
			}

			// First 4 bytes after string is the image count.
			var imageCount = s.ReadInt16();
			if (imageCount == 0)
			{
				s.Position = start;
				return false;
			}

			s.Position = start;
			return true;
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames)
		{
			if (!IsShpSSI(s))
			{
				frames = null;
				return false;
			}

			additionalByteOffset = isMechCommanderSSI ? 6 : 0;
			frames = ParseFrames(s);
			return true;
		}

		ShpSSIFrame[] ParseFrames(Stream s)
		{
			var start = s.Position;
			s.Position = 4 + additionalByteOffset;

			// Read frame count and offsets in the file.
			var frameCount = s.ReadUInt32();
			frameOffsets = new uint[frameCount];
			for (var i = 0; i < frameCount; i++)
			{
				frameOffsets[i] = s.ReadUInt32();
				/*framePalette[i] =*/ s.ReadUInt32();
			}

			// Current stream position should be 152 + additionalByteOffset
			var width = s.ReadUInt16();
			var height = s.ReadUInt16();
			var size = new Size(width, height);

			var frames = new ShpSSIFrame[frameCount];
			for (var i = 0; i < frames.Length; i++)
			{
				s.Position = frameOffsets[i] + additionalByteOffset;
				frames[i] = new ShpSSIFrame(s, size);
			}

			s.Position = start;
			return frames;
		}

		class ShpSSIFrame : ISpriteFrame
		{
			const int HeaderSize = 24;
			const byte BackColor = 0;

			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			int pix_pos = 0;

			public ShpSSIFrame(Stream s, Size frameSize)
			{
				var xSize = s.ReadUInt16();
				var ySize = s.ReadUInt16();
				// These are the same for all SSI SHPs and can probably be ignored
				var xOffset = s.ReadUInt16();
				var yOffset = s.ReadUInt16();
				var left = s.ReadInt32();
				var top = s.ReadInt32();
				var right = s.ReadInt32();
				var bottom = s.ReadInt32();

				// These are apparently not dimensions, but maximum indices.
				// We add 1 so we can use them as dimensions.
				var width = right - left + 1;
				var height = bottom - top + 1;

				Size = new Size(width, height);
				FrameSize = frameSize;
				Offset = new float2((left + right) / 2, (top + bottom) / 2);

				int l; // first line the counter
				int lf; // last line

				var pixelIndex = 0;

				// TODO: Waste less memory for this. It can be much less!
				Data = new byte[frameSize.Width*frameSize.Height];
				//Data = new byte[width*height];

				if (top < 0)
				{
					l = 0;
					lf = bottom + Math.Abs(top);
				}
				else
				{
					l = top;
					lf = bottom;
				}

				pix_pos = left < 0 ? 0 : left;

				do
				{
					var ch = s.ReadUInt8();
					var r = ch%2;
					var b = ch/2;

					if (b == 0 && r == 1) // a skip over
					{
						ch = s.ReadUInt8();
						for (var i = 0; i < ch; ++i)
							Data[pixelIndex++] = BackColor; //put_pix(l, BACK_COLOR);
					}
					else if (b == 0)   // end of line
					{
						++l;
						while ((pixelIndex % width) != 0)
							Data[pixelIndex++] = BackColor;

						pix_pos = left < 0 ? 0 : left;
					}
					else if (r == 0) // a run of bytes
					{
						ch = s.ReadUInt8(); // the color #
						for (var i = 0; i < b; ++i)
							Data[pixelIndex++] = ch; //put_pix(l, ch);
					}
					else // b==0 and r==1 ... read the next b bytes as color #'s
					{
						for (var i = 0; i < b; ++i)
						{
							ch = s.ReadUInt8();
							Data[pixelIndex++] = ch; //put_pix(l, ch);
						}
					}
				}
				while (l <= lf);
			}
		}
	}
}
