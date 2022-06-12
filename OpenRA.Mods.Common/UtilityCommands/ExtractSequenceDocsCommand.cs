using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.UtilityCommands
{
	class ExtractSequenceDocsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--sequence-docs";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return true;
		}

		[Desc("[VERSION]", "Generate sprite sequence documentation in JSON format.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var version = utility.ModData.Manifest.Metadata.Version;
			if (args.Length > 1)
				version = args[1];

			var objectCreator = utility.ModData.ObjectCreator;
			var sequenceTypes = objectCreator.GetTypesImplementing<ISpriteSequence>().OrderBy(t => t.Namespace);

			var json = GenerateJson(version, sequenceTypes);
			Console.WriteLine(json);
		}

		static string GenerateJson(string version, IEnumerable<Type> sequenceTypes)
		{
			var relatedEnumTypes = new HashSet<Type>();

			var sequenceTypesInfo = sequenceTypes
				.Where(x => x.GetCustomAttributes<DescAttribute>(false).Length > 0)
				.Select(type => new
				{
					Namespace = type.Namespace,
					Name = type.Name,
					Description = string.Join(" ", type.GetCustomAttributes<DescAttribute>(false).SelectMany(d => d.Lines)),
					InheritedTypes = type.BaseTypes()
						.Select(y => y.Name)
						.Where(y => y != type.Name && y != "Object"),
					Properties = type.GetProperties()
						.Where(propInfo => propInfo.GetCustomAttributes<DescAttribute>(false).Length > 0)
						.Select(propInfo =>
						{
							if (propInfo.PropertyType.IsEnum)
								relatedEnumTypes.Add(propInfo.PropertyType);

							return new
							{
								PropertyName = propInfo.Name,
								DefaultValue = propInfo.GetValue(Activator.CreateInstance(type))?.ToString(),
								Type = Util.FriendlyTypeName(propInfo.PropertyType),
								InternalType = Util.InternalTypeName(propInfo.PropertyType),
								UserFriendlyType = Util.FriendlyTypeName(propInfo.PropertyType),
								Description = string.Join(" ", propInfo.GetCustomAttributes<DescAttribute>(false).SelectMany(d => d.Lines))
							};
						})
				});

			var relatedEnums = relatedEnumTypes.Select(type => new
			{
				Namespace = type.Namespace,
				Name = type.Name,
				Values = Enum.GetNames(type).Select(x => new
				{
					Key = x,
					Value = Convert.ToInt32(Enum.Parse(type, x))
				})
			});

			var result = new
			{
				Version = version,
				SequenceTypes = sequenceTypesInfo,
				RelatedEnums = relatedEnums
			};

			return Newtonsoft.Json.JsonConvert.SerializeObject(result);
		}
	}
}
