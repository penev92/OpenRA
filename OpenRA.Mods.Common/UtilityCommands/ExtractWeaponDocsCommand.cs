#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
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
using System.Text;
using Newtonsoft.Json;
using OpenRA.GameRules;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.UtilityCommands
{
	class ExtractWeaponDocsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--weapon-docs";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return true;
		}

		[Desc("[VERSION]", "Generate weaponry documentation in MarkDown format.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var version = utility.ModData.Manifest.Metadata.Version;
			if (args.Length > 1)
				version = args[1];

			var objectCreator = utility.ModData.ObjectCreator;
			var weaponInfo = new[] { typeof(WeaponInfo) };
			var warheads = objectCreator.GetTypesImplementing<IWarhead>().OrderBy(t => t.Namespace);
			var projectiles = objectCreator.GetTypesImplementing<IProjectileInfo>().OrderBy(t => t.Namespace);

			var weaponTypes = weaponInfo.Concat(projectiles).Concat(warheads);

			if (args.Length > 2 && args[2].ToLowerInvariant() == "json")
				GenerateJson(version, weaponTypes, objectCreator);
			else
				GenerateMarkdown(version, weaponTypes, objectCreator);
		}

		static void GenerateMarkdown(string version, IEnumerable<Type> weaponTypes, ObjectCreator objectCreator)
		{
			var doc = new StringBuilder();

			doc.AppendLine(
				"This documentation is aimed at modders. It displays a template for weapon definitions " +
				"as well as its contained types (warheads and projectiles) with default values and developer commentary. " +
				"Please do not edit it directly, but add new `[Desc(\"String\")]` tags to the source code. This file has been " +
				$"automatically generated for version {version} of OpenRA.");
			doc.AppendLine();

			var currentNamespace = "";

			foreach (var t in weaponTypes)
			{
				// skip helpers like TraitInfo<T>
				if (t.ContainsGenericParameters || t.IsAbstract)
					continue;

				if (currentNamespace != t.Namespace)
				{
					currentNamespace = t.Namespace;
					doc.AppendLine();
					doc.AppendLine($"## {currentNamespace}");
				}

				var traitName = t.Name.EndsWith("Info") ? t.Name.Substring(0, t.Name.Length - 4) : t.Name;
				doc.AppendLine();
				doc.AppendLine($"### {traitName}");

				var traitDescLines = t.GetCustomAttributes<DescAttribute>(false).SelectMany(d => d.Lines);
				foreach (var line in traitDescLines)
					doc.AppendLine(line);

				var infos = FieldLoader.GetTypeLoadInfo(t);
				if (!infos.Any())
					continue;

				doc.AppendLine();
				doc.AppendLine("| Property | Default Value | Type | Description |");
				doc.AppendLine("| -------- | ------------- | ---- | ----------- |");

				var liveTraitInfo = objectCreator.CreateBasic(t);
				foreach (var info in infos)
				{
					var defaultValue = FieldSaver.SaveField(liveTraitInfo, info.Field.Name).Value.Value;
					var fieldType = Util.FriendlyTypeName(info.Field.FieldType);
					var fieldDescLines = info.Field.GetCustomAttributes<DescAttribute>(true).SelectMany(d => d.Lines);

					doc.AppendLine($"| {info.YamlName} | {defaultValue} | {fieldType} | {string.Join(" ", fieldDescLines)} |");
				}
			}

			Console.Write(doc.ToString());
		}

		static void GenerateJson(string version, IEnumerable<Type> weaponTypes, ObjectCreator objectCreator)
		{
			var weaponTypesInfo = weaponTypes.Where(x => !x.ContainsGenericParameters && !x.IsAbstract)
				.Select(type => new
				{
					Namespace = type.Namespace,
					Name = type.Name.EndsWith("Info") ? type.Name.Substring(0, type.Name.Length - 4) : type.Name,
					Description = string.Join(" ", type.GetCustomAttributes<DescAttribute>(false).SelectMany(d => d.Lines)),
					InheritedTypes = type.BaseTypes()
						.Select(y => y.Name)
						.Where(y => y != type.Name && y != $"{type.Name}Info" && y != "Object"),
					Properties = FieldLoader.GetTypeLoadInfo(type)
						.Where(fi => fi.Field.IsPublic && fi.Field.IsInitOnly && !fi.Field.IsStatic)
						.Select(fi => new
						{
							PropertyName = fi.YamlName,
							DefaultValue = FieldSaver.SaveField(objectCreator.CreateBasic(type), fi.Field.Name).Value.Value,
							InternalType = Util.InternalTypeName(fi.Field.FieldType),
							UserFriendlyType = Util.FriendlyTypeName(fi.Field.FieldType),
							Description = string.Join(" ", fi.Field.GetCustomAttributes<DescAttribute>(true).SelectMany(d => d.Lines)),
							OtherAttributes = fi.Field.CustomAttributes
								.Where(a => a.AttributeType.Name != nameof(DescAttribute) && a.AttributeType.Name != nameof(FieldLoader.LoadUsingAttribute))
								.Select(a =>
								{
									var name = a.AttributeType.Name;
									name = name.EndsWith("Attribute") ? name.Substring(0, name.Length - 9) : name;

									return new
									{
										Name = name,
										Value = a.ConstructorArguments.Select(b => b.Value)
									};
								})
						})
				});

			var result = new
			{
				Version = version,
				WeaponTypes = weaponTypesInfo
			};

			var serializedResult = JsonConvert.SerializeObject(result);

			Console.WriteLine(serializedResult);
		}
	}
}
