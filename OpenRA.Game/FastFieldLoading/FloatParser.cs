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
using System.Globalization;
using System.Numerics;

namespace OpenRA.FastFieldLoading
{
	public readonly struct FloatParser<T> : ISpanParser<T>
		where T : INumberBase<T>
	{
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out T value)
			=> T.TryParse(s, NumberStyles.Float, provider, out value);
	}
}
