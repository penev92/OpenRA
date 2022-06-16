using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA
{
	public class ProfanityFilter
	{
		readonly string[] profanities;

		public ProfanityFilter(string[] profanityPaths, IReadOnlyFileSystem fileSystem)
		{
			profanities = ReadFiles(profanityPaths, fileSystem).ToArray();
		}

		public string GetSanitizedString(string text)
		{
			var sanitizedString = text;
			foreach (var profanity in profanities)
				sanitizedString = sanitizedString.Replace(profanity, "$#@!", StringComparison.InvariantCultureIgnoreCase);

			return sanitizedString;
		}

		IEnumerable<string> ReadFiles(string[] profanityPaths, IReadOnlyFileSystem fileSystem)
		{
			foreach (var path in profanityPaths)
			{
				var stream = fileSystem.Open(path);
				using (var reader = new StreamReader(stream))
				{
					while (!reader.EndOfStream)
					{
						yield return reader.ReadLine();
					}
				}
			}
		}
	}
}
