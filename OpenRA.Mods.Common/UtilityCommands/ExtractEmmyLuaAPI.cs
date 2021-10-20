#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
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
using System.Reflection;
using OpenRA.Scripting;

namespace OpenRA.Mods.Common.UtilityCommands
{
	// See https://emmylua.github.io/annotation.html for reference
	class ExtractEmmyLuaAPI : IUtilityCommand
	{
		string IUtilityCommand.Name => "--emmy-lua-api";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return true;
		}

		[Desc("Generate EmmyLua API annotations for use in IDEs.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var version = Game.ModData.Manifest.Metadata.Version;
			Console.WriteLine($"-- This is an automatically generated Lua API definition generated for {version} of OpenRA.");
			Console.WriteLine("-- https://wiki.openra.net/Utility was used with the --emmy-lua-api parameter.");
			Console.WriteLine("-- See https://docs.openra.net/en/latest/release/lua/ for human readable documentation.");

			Console.WriteLine();
			WriteManual();
			Console.WriteLine();

			var globalTables = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptGlobal>().OrderBy(t => t.Name);
			WriteGlobals(globalTables);

			Console.WriteLine();

			var actorProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptActorProperties>();
			WriteScriptProperties(typeof(Actor), actorProperties);

			var playerProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptPlayerProperties>();
			WriteScriptProperties(typeof(Player), playerProperties);
		}

		static void WriteManual()
		{
			Console.WriteLine("--- This function is triggered once, after the map is loaded.");
			Console.WriteLine("function WorldLoaded() end");
			Console.WriteLine();
			Console.WriteLine("--- This function will hit every game tick which by default is every 40 ms.");
			Console.WriteLine("function Tick() end");
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("--- These are engine base types.");
			Console.WriteLine("---@class cpos");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine();
			Console.WriteLine("---@class wpos");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine("---@field Z integer");
			Console.WriteLine();
			Console.WriteLine("---@class wangle");
			Console.WriteLine("---@field Angle integer");
			Console.WriteLine();
			Console.WriteLine("---@class wdist");
			Console.WriteLine("---@field Length integer");
			Console.WriteLine();
			Console.WriteLine("---@class wvec");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine("---@field Z integer");
			Console.WriteLine();
			Console.WriteLine("---@class cvec");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine();
			Console.WriteLine("---@class color");
			Console.WriteLine("local color = { };");
			Console.WriteLine();
		}

		static void WriteGlobals(IEnumerable<Type> globalTables)
		{
			foreach (var t in globalTables)
			{
				var name = t.GetCustomAttributes<ScriptGlobalAttribute>(true).First().Name;
				Console.WriteLine("---Global variable provided by the game scripting engine.");

				foreach (var obsolete in t.GetCustomAttributes(false).OfType<ObsoleteAttribute>())
				{
					Console.WriteLine("---@deprecated");
					Console.WriteLine($"--- {obsolete.Message}");
				}

				Console.WriteLine(name + " = {");

				var members = ScriptMemberWrapper.WrappableMembers(t);
				foreach (var member in members.OrderBy(m => m.Name))
				{
					Console.WriteLine();

					var body = "";

					if (member.HasAttribute<DescAttribute>())
					{
						var lines = member.GetCustomAttributes<DescAttribute>(true).First().Lines;
						foreach (var line in lines)
							Console.WriteLine($"    --- {line}");
					}

					var propertyInfo = member as PropertyInfo;
					if (propertyInfo != null)
					{
						var attributes = propertyInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"    ---@deprecated {obsolete.Message}");

						Console.WriteLine($"    ---@type {propertyInfo.PropertyType.EmmyLuaString()}");
						body = propertyInfo.Name + " = { };";
					}

					var methodInfo = member as MethodInfo;
					if (methodInfo != null)
					{
						var parameters = methodInfo.GetParameters();
						foreach (var parameter in parameters)
							Console.WriteLine($"    ---@param {parameter.EmmyLuaString()}");

						var parameterString = parameters.Select(p => p.Name).JoinWith(", ");

						var attributes = methodInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"    ---@deprecated {obsolete.Message}");

						var returnType = methodInfo.ReturnType.EmmyLuaString();
						if (returnType != "Void")
							Console.WriteLine($"    ---@return {returnType}");

						body = member.Name + $" = function({parameterString}) end;";
					}

					Console.WriteLine($"    {body}");
				}

				Console.WriteLine("}");
				Console.WriteLine();
			}
		}

		static void WriteScriptProperties(Type type, IEnumerable<Type> implementingTypes)
		{
			var className = type.Name.ToLowerInvariant();
			var tableName = $"__{type.Name.ToLowerInvariant()}";
			Console.WriteLine($"---@class {className}");
			Console.WriteLine("local " + tableName + " = {");

			var properties = implementingTypes.SelectMany(t =>
			{
				var required = ScriptMemberWrapper.RequiredTraitNames(t);
				return ScriptMemberWrapper.WrappableMembers(t).Select(memberInfo => (memberInfo, required));
			});

			foreach (var property in properties)
			{
				Console.WriteLine();

				var isActivity = property.memberInfo.HasAttribute<ScriptActorPropertyActivityAttribute>();

				if (property.memberInfo.HasAttribute<DescAttribute>())
				{
					var lines = property.memberInfo.GetCustomAttributes<DescAttribute>(true).First().Lines;
					foreach (var line in lines)
						Console.WriteLine($"    --- {line}");
				}

				if (isActivity)
					Console.WriteLine("    --- *Queued Activity*");

				if (property.required.Any())
					Console.WriteLine($"    --- **Requires {(property.required.Length == 1 ? "Trait" : "Traits")}:** {property.required.Select(GetDocumentationUrl).JoinWith(", ")}");

				var methodInfo = property.memberInfo as MethodInfo;
				if (methodInfo != null)
				{
					var attributes = methodInfo.GetCustomAttributes(false);
					foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
						Console.WriteLine($"    ---@deprecated {obsolete.Message}");

					var parameters = methodInfo.GetParameters();
					foreach (var parameter in parameters)
						Console.WriteLine($"    ---@param {parameter.EmmyLuaString()}");

					var parameterString = parameters.Select(p => p.Name).JoinWith(", ");

					var returnType = methodInfo.ReturnType.EmmyLuaString();
					if (returnType != "Void")
						Console.WriteLine($"    ---@return {returnType}");

					Console.WriteLine($"    {methodInfo.Name} = function({parameterString}) end;");
				}

				var propertyInfo = property.memberInfo as PropertyInfo;
				if (propertyInfo != null)
				{
					Console.WriteLine($"    ---@type {propertyInfo.PropertyType.EmmyLuaString()}");
					Console.WriteLine("    " + propertyInfo.Name + " = { };");
				}
			}

			Console.WriteLine("}");
			Console.WriteLine();
		}

		static string GetDocumentationUrl(string trait)
		{
			return $"[{trait}](https://docs.openra.net/en/latest/release/traits/#{trait.ToLowerInvariant()})";
		}
	}

	public static class EmmyLuaExts
	{
		static readonly Dictionary<string, string> LuaTypeNameReplacements = new Dictionary<string, string>()
		{
			{ "Int32", "integer" },
			{ "String", "string" },
			{ "String[]", "string[]" },
			{ "Boolean", "boolean" },
			{ "Double", "number" },
			{ "Object", "any" },
			{ "LuaTable", "table" },
			{ "LuaValue", "any" },
			{ "LuaValue[]", "table" },
			{ "LuaFunction", "function" },
			{ "WVec", "wvec" },
			{ "CVec", "cvec" },
			{ "CPos", "cpos" },
			{ "CPos[]", "cpos[]" },
			{ "WPos", "wpos" },
			{ "WAngle", "wangle" },
			{ "WAngle[]", "wangle[]" },
			{ "WDist", "wdist" },
			{ "Color", "color" },
			{ "Actor", "actor" },
			{ "Actor[]", "actor[]" },
			{ "Player", "player" },
			{ "Player[]", "player[]" },
		};

		public static string EmmyLuaString(this Type type)
		{
			if (!LuaTypeNameReplacements.TryGetValue(type.Name, out var replacement))
				replacement = type.Name;

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				var argument = type.GetGenericArguments().Select(p => p.Name).First();
				if (LuaTypeNameReplacements.TryGetValue(argument, out var genericReplacement))
					replacement = $"{genericReplacement}?";
				else
					replacement = $"{type.GetGenericArguments().Select(p => p.Name).First()}?";
			}

			return replacement;
		}

		public static string EmmyLuaString(this ParameterInfo parameterInfo)
		{
			var optional = parameterInfo.IsOptional ? "?" : "";

			var parameterType = parameterInfo.ParameterType.EmmyLuaString();

			// A hack for ActorGlobal.Create().
			if (parameterInfo.Name == "initTable")
				parameterType = "initTable";

			return $"{parameterInfo.Name}{optional} {parameterType}";
		}

		public static string EmmyLuaString(this PropertyInfo propertyInfo)
		{
			return $"{propertyInfo.Name} {propertyInfo.PropertyType.EmmyLuaString()}";
		}
	}
}
