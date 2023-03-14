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
using System.Linq;
using Newtonsoft.Json;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.UtilityCommands
{
	class ExtractWidgetDocsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--widget-docs";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return true;
		}

		[Desc("[VERSION]", "Generate widget and widget documentation in JSON format.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var version = utility.ModData.Manifest.Metadata.Version;
			if (args.Length > 1)
				version = args[1];

			var objectCreator = utility.ModData.ObjectCreator;
			var widgetTypes = objectCreator.GetTypesImplementing<Widget>().OrderBy(t => t.Namespace).ThenBy(t => t.Name);

			var json = GenerateJson(version, widgetTypes);
			Console.WriteLine(json);
		}

		static string GenerateJson(string version, IEnumerable<Type> widgetTypes)
		{
			var relatedEnumTypes = new HashSet<Type>();

			var widgetTypesInfo = widgetTypes
				.Where(x => !x.ContainsGenericParameters && !x.IsAbstract)
				.Select(type =>
				{
					// Exclude any Func<> fields, but don't exclude any collections.
					var properties = FieldLoader.GetTypeLoadInfo(type)
						.Where(fi => fi.Field.IsPublic && !fi.Field.IsStatic
							&& (!fi.Field.FieldType.IsGenericType || fi.Field.FieldType.GetInterfaces().Contains(typeof(System.Collections.IEnumerable))));

					var propertyData = properties
						.Select(fi =>
						{
							if (fi.Field.FieldType.IsEnum)
								relatedEnumTypes.Add(fi.Field.FieldType);

							return new
							{
								PropertyName = fi.YamlName,
								InternalType = Util.InternalTypeName(fi.Field.FieldType),
								UserFriendlyType = Util.FriendlyTypeName(fi.Field.FieldType),
								Description = string.Join(" ", fi.Field.GetCustomAttributes<DescAttribute>(true).SelectMany(d => d.Lines))
							};
						});

					return new
					{
						type.Namespace,
						type.Name,
						Description = string.Join(" ", type.GetCustomAttributes<DescAttribute>(false).SelectMany(d => d.Lines)),
						InheritedTypes = type.BaseTypes()
							.Select(y => y.Name)
							.Where(y => y != type.Name && y != "Object"),
						Properties = propertyData
					};
				});

			var relatedEnums = relatedEnumTypes.OrderBy(t => t.Name).Select(type => new
			{
				type.Namespace,
				type.Name,
				Values = Enum.GetNames(type).Select(x => new
				{
					Key = Convert.ToInt32(Enum.Parse(type, x)),
					Value = x
				})
			});

			var result = new
			{
				Version = version,
				WidgetTypes = widgetTypesInfo.ToArray(),
				RelatedEnums = relatedEnums
			};

			return JsonConvert.SerializeObject(result);
		}
	}
}
