using System.Collections.Generic;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA.MiniYamlParser
{
	public static class CustomMiniYamlLoader
	{
		public static List<MiniYamlNode> FromPackage(IReadOnlyFileSystem fileSystem, IEnumerable<string> files, MiniYaml mapRules)
		{
			if (mapRules != null && mapRules.Value != null)
			{
				var mapFiles = FieldLoader.GetValue<string[]>("value", mapRules.Value);
				files = files.Append(mapFiles);
			}

			var yaml = files.Select(s => MiniYamlLoader.FromStream(fileSystem.Open(s), s));
			if (mapRules != null && mapRules.Nodes.Any())
				yaml = yaml.Append(mapRules.Nodes);

			return MiniYamlMerger.Merge(yaml);
		}
	}
}
