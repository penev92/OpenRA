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
using System.Globalization;

namespace OpenRA.FastFieldLoading
{
	public static class FastFieldLoader
	{
		const char Comma = ',';

		public static T Load<T, TParser>(Dictionary<string, MiniYaml> dict, string fieldName, T fallbackValue)
			where TParser : struct, ISpanParser<T>
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				var s = node.Value.AsSpan();
				if (TParser.TryParse(s, CultureInfo.InvariantCulture, out var value))
					return value;
			}

			return fallbackValue;
		}

		/// <summary>
		/// Special fast-path overload even if everything else is generic
		/// because if we go through the generic route we'll end up allocating unnecessary strings.
		/// </summary>
		public static string LoadString(Dictionary<string, MiniYaml> dict, string fieldName, string fallbackValue)
		{
			if (dict.TryGetValue(fieldName, out var node))
				return node.Value;

			return fallbackValue;
		}

		public static T[] LoadArray<T, TParser>(Dictionary<string, MiniYaml> dict, string fieldName, T[] fallbackValue, int elementArity = 1)
			where TParser : struct, ISpanParser<T>
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				if (node.Value == null)
					return [];

				var span = node.Value.AsSpan();
				var n = CountItems(span);
				if (n == 0)
					return [];

				if (n % elementArity != 0)
					throw new YamlException("");

				var result = new T[n / elementArity];
				var i = 0;
				while (NextToken(ref span, elementArity, out var tokenAsSpan))
				{
					if (!TParser.TryParse(tokenAsSpan, CultureInfo.InvariantCulture, out result[i++]))
						throw new YamlException("");
				}

				return result;
			}

			return fallbackValue;
		}

		public static string[] LoadArray(Dictionary<string, MiniYaml> dict, string fieldName, string[] fallbackValue)
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				if (node.Value == null)
					return [];

				var span = node.Value.AsSpan();
				var n = CountItems(span);
				if (n == 0)
					return [];

				var result = new string[n];
				var i = 0;
				while (NextToken(ref span, 1, out var tokenAsSpan))
					result[i++] = tokenAsSpan.ToString();

				return result;
			}

			return fallbackValue;
		}

		public static HashSet<T> LoadHashSet<T, TParser>(Dictionary<string, MiniYaml> dict, string fieldName, HashSet<T> fallbackValue, int elementArity)
			where TParser : struct, ISpanParser<T>
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				if (node.Value == null)
					return [];

				var span = node.Value.AsSpan();
				var n = CountItems(span);
				if (n == 0)
					return [];

				if (n % elementArity != 0)
					throw new YamlException("");

				var set = new HashSet<T>(n);
				while (NextToken(ref span, elementArity, out var tokenAsSpan))
				{
					if (!TParser.TryParse(tokenAsSpan, CultureInfo.InvariantCulture, out var result))
						throw new YamlException("");

					set.Add(result);
				}

				return set;
			}

			return fallbackValue;
		}

		public static HashSet<string> LoadHashSet(Dictionary<string, MiniYaml> dict, string fieldName, HashSet<string> fallbackValue)
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				if (node.Value == null)
					return [];

				var span = node.Value.AsSpan();
				var n = CountItems(span);
				if (n == 0)
					return [];

				var set = new HashSet<string>(n);
				while (NextToken(ref span, 1, out var tokenAsSpan))
					set.Add(tokenAsSpan.ToString());

				return set;
			}

			return fallbackValue;
		}

		public static Dictionary<TKey, TValue> LoadDictionary<TKey, TValue, TKeyParser, TValueParser>(
			Dictionary<string, MiniYaml> dict,
			string fieldName,
			Dictionary<TKey, TValue> fallbackValue,
			IEqualityComparer<TKey>? comparer = null)
			where TKeyParser : struct, ISpanParser<TKey>
			where TValueParser : struct, ISpanParser<TValue>
		{
			if (dict.TryGetValue(fieldName, out var node))
			{
				var result = new Dictionary<TKey, TValue>(node.Nodes.Length);
				foreach(var subnode in node.Nodes)
				{
					if (!TKeyParser.TryParse(subnode.Key, CultureInfo.InvariantCulture, out var key))
						throw new YamlException("");

					if (!TValueParser.TryParse(subnode.Value.Value, CultureInfo.InvariantCulture, out var value))
						throw new YamlException("");

					result.Add(key, value);
				}

				return result;
			}

			return fallbackValue;

			//var span = node.Value.AsSpan();
			//int n = CountItems(span);

			//var d = comparer is null ? new Dictionary<TKey, TValue>(n) : new Dictionary<TKey, TValue>(n, comparer);

			//while (NextToken(ref span, out var pairTok))
			//{
			//	int eq = pairTok.IndexOf(keyValueSeparator);
			//	if (eq <= 0 || eq == pairTok.Length - 1)
			//		return fallback;

			//	var kTok = pairTok[..eq].Trim();
			//	var vTok = pairTok[(eq + 1)..].Trim();

			//	if (!TKeyParser.TryParse(kTok, CultureInfo.InvariantCulture, out var key)) return fallback;
			//	if (!TValueParser.TryParse(vTok, CultureInfo.InvariantCulture, out var val)) return fallback;

			//	d[key] = val;
			//}

			//return d;
		}

		static int CountItems(ReadOnlySpan<char> s)
		{
			int count = 0;
			bool inToken = false;

			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] == Comma)
				{
					if (inToken) { count++; inToken = false; }
				}
				else if (!char.IsWhiteSpace(s[i]))
				{
					inToken = true;
				}
			}

			if (inToken) count++;
			return count;
		}

		static bool NextToken(ref ReadOnlySpan<char> s, int tokenCount, out ReadOnlySpan<char> token)
		{
			s = s.TrimStart();
			if (s.IsEmpty) { token = default; return false; }

			int cut = -1;
			int searchFrom = 0;

			// Find the position of the tokenCount-th comma.
			for (int n = 0; n < tokenCount; n++)
			{
				int comma = s[searchFrom..].IndexOf(Comma);
				if (comma < 0)
				{
					// Not enough commas left -> take the rest.
					token = s.Trim();
					s = default;
					return token.Length != 0;
				}

				cut = searchFrom + comma;
				searchFrom = cut + 1;
			}

			// Token contains tokenCount subtokens with commas between them.
			token = s[..cut].Trim();
			s = s[(cut + 1)..];   // Move past the comma that ended this group.
			return token.Length != 0;
		}
	}
}
