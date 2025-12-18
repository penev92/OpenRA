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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OpenRA
{
	[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Mimic a built-in type alias.")]
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct float3 : IEquatable<float3>, ISpanParsable<float3>
	{
		public readonly float X, Y, Z;
		public float2 XY => new(X, Y);

		public float3(float x, float y, float z) { X = x; Y = y; Z = z; }
		public float3(float2 xy, float z) { X = xy.X; Y = xy.Y; Z = z; }

		public static implicit operator float3(int2 src) { return new float3(src.X, src.Y, 0); }
		public static implicit operator float3(float2 src) { return new float3(src.X, src.Y, 0); }

		public static float3 operator +(in float3 a, in float3 b) { return new float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
		public static float3 operator -(in float3 a, in float3 b) { return new float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
		public static float3 operator -(in float3 a) { return new float3(-a.X, -a.Y, -a.Z); }
		public static float3 operator *(in float3 a, in float3 b) { return new float3(a.X * b.X, a.Y * b.Y, a.Z * b.Z); }
		public static float3 operator *(float a, in float3 b) { return new float3(a * b.X, a * b.Y, a * b.Z); }
		public static float3 operator /(in float3 a, in float3 b) { return new float3(a.X / b.X, a.Y / b.Y, a.Z / b.Z); }
		public static float3 operator /(in float3 a, float b) { return new float3(a.X / b, a.Y / b, a.Z / b); }

		public static float3 Lerp(float3 a, float3 b, float t)
		{
			return new float3(
				float2.Lerp(a.X, b.X, t),
				float2.Lerp(a.Y, b.Y, t),
				float2.Lerp(a.Z, b.Z, t));
		}

		public static bool operator ==(in float3 me, in float3 other) { return me.X == other.X && me.Y == other.Y && me.Z == other.Z; }
		public static bool operator !=(in float3 me, in float3 other) { return !(me == other); }
		public override int GetHashCode() { return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode(); }

		public bool Equals(float3 other)
		{
			return other == this;
		}

		public override bool Equals(object obj)
		{
			return obj is float3 o && (float3?)o == this;
		}

		public override string ToString() { return $"{X},{Y},{Z}"; }

		#region ISpanParsable<>

		public static float3 Parse(ReadOnlySpan<char> s, IFormatProvider provider)
			=> TryParse(s, provider, out var v)
				? v
				: throw new FormatException($"Invalid float3: '{s.ToString()}'");

		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, [MaybeNullWhen(false)] out float3 result)
		{
			result = default;

			var c1 = s.IndexOf(',');
			if (c1 <= 0) return false;

			var rest = s[(c1 + 1)..];
			var c2rel = rest.IndexOf(',');
			if (c2rel <= 0) return false;

			var c2 = c1 + 1 + c2rel;

			var xs = s[..c1].Trim();
			var ys = s[(c1 + 1)..c2].Trim();
			var zs = s[(c2 + 1)..].Trim();

			// reject extra commas like "1,2,3,4"
			if (zs.IndexOf(',') >= 0) return false;

			const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;

			if (!float.TryParse(xs, styles, provider, out var x)) return false;
			if (!float.TryParse(ys, styles, provider, out var y)) return false;
			if (!float.TryParse(zs, styles, provider, out var z)) return false;

			result = new float3(x, y, z);
			return true;
		}

		public static float3 Parse(string s, IFormatProvider provider)
			=> Parse(s.AsSpan(), provider);

		public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out float3 result)
			=> TryParse(s.AsSpan(), provider, out result);

		#endregion

		public static readonly float3 Zero = new(0, 0, 0);
		public static readonly float3 Ones = new(1, 1, 1);
	}
}
