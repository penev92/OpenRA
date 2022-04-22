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
using System.Text.RegularExpressions;
using Linguini.Shared.Types.Bundle;

namespace OpenRA.Network
{
	public class FluentArgument
	{
		[Flags]
		public enum FluentArgumentType
		{
			String = 0,
			Number = 1,
		}

		public readonly string Key;
		public readonly string Value;
		public readonly FluentArgumentType Type;

		public FluentArgument() { }

		public FluentArgument(string key, object value)
		{
			Key = key;
			Value = value.ToString();
			Type = GetFluentArgumentType(value);
		}

		static FluentArgumentType GetFluentArgumentType(object value)
		{
			switch (value.ToFluentType())
			{
				case FluentNumber _:
					return FluentArgumentType.Number;
				default:
					return FluentArgumentType.String;
			}
		}
	}

	public class LocalizedMessage
	{
		public const int ProtocolVersion = 1;

		public readonly string Key;

		[FieldLoader.LoadUsing(nameof(LoadArguments))]
		public readonly FluentArgument[] Arguments;

		static object LoadArguments(MiniYaml yaml)
		{
			var arguments = new List<FluentArgument>();
			var argumentsNode = yaml.Nodes.FirstOrDefault(n => n.Key == "Arguments");
			if (argumentsNode != null)
			{
				var regex = new Regex(@"Argument@\d+");
				foreach (var argument in argumentsNode.Value.Nodes)
					if (regex.IsMatch(argument.Key))
						arguments.Add(FieldLoader.Load<FluentArgument>(argument.Value));
			}

			return arguments.ToArray();
		}

		static readonly string[] SerializeFields = { "Key" };

		public LocalizedMessage(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
		}

		public LocalizedMessage(string key, Dictionary<string, object> arguments = null)
		{
			Key = key;
			Arguments = arguments?.Select(a => new FluentArgument(a.Key, a.Value)).ToArray();
		}

		public string Serialize()
		{
			var root = new List<MiniYamlNode>() { new MiniYamlNode("Protocol", ProtocolVersion.ToString()) };
			foreach (var field in SerializeFields)
				root.Add(FieldSaver.SaveField(this, field));

			if (Arguments != null)
			{
				var argumentsNode = new MiniYaml("");
				var i = 0;
				foreach (var argument in Arguments)
					argumentsNode.Nodes.Add(new MiniYamlNode("Argument@" + i++, FieldSaver.Save(argument)));

				root.Add(new MiniYamlNode("Arguments", argumentsNode));
			}

			return new MiniYaml("", root)
				.ToLines("LocalizedMessage")
				.JoinWith("\n");
		}

		public string Translate(ModData modData)
		{
			var argumentDictionary = new Dictionary<string, object>();
			foreach (var argument in Arguments)
			{
				if (argument.Type == FluentArgument.FluentArgumentType.Number)
				{
					if (!int.TryParse(argument.Value, out var number))
						Log.Write("debug", $"Failed to parse {argument.Value}");

					argumentDictionary.Add(argument.Key, number);
				}
				else
					argumentDictionary.Add(argument.Key, argument.Value);
			}

			return modData.Translation.GetString(Key, argumentDictionary);
		}
	}
}
