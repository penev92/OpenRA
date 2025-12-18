using System;

namespace OpenRA.FastFieldLoading
{
	/// <summary>
	/// ISpanParser implementation for string-related edge cases.
	/// Use when necessary, but prefer a string-specific code path instead when possible.
	/// </summary>
	public readonly struct StringParser : ISpanParser<string>
	{
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out string value)
		{
			value = s.ToString();   // Allocation per token is unavoidable.
			return true;
		}
	}
}
