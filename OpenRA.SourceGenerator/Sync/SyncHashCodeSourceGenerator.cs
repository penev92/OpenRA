﻿#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.SourceGenerators.Sync
{
	// https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/
	[Generator]
	public class SyncHashCodeSourceGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var classDeclarationsProvider = context.SyntaxProvider
				.CreateSyntaxProvider(
					predicate: static (syntaxNode, _) => SourceGeneratorHelpers.IsPartialClass(syntaxNode),
					transform: static (generatorContext, _) => GetClassDeclarationSymbol(generatorContext))
				.Where(x => x != null)
				.Collect();

			var compilationAndClasses = context.CompilationProvider.Combine(classDeclarationsProvider);

			context.RegisterSourceOutput(compilationAndClasses,
				static (context, source) => GenerateCode(source.Left, source.Right, context));
		}

		static ClassDeclarationSyntax GetClassDeclarationSymbol(GeneratorSyntaxContext context)
		{
			if (context.SemanticModel.GetDeclaredSymbol(context.Node) is INamedTypeSymbol classSymbol && classSymbol.ImplementsISync())
				return context.Node as ClassDeclarationSyntax;

			return null;
		}

		static void GenerateCode(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
		{
			if (classes.IsDefaultOrEmpty)
				return;

			foreach (var classDeclaration in classes.Distinct())
			{
				var (filename, content) = ProcessType(classDeclaration, compilation);
				if (content != null)
					context.AddSource(filename, content);
			}
		}

		static (string FileName, string Content) ProcessType(ClassDeclarationSyntax classDeclaration, Compilation compilation)
		{
			if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceDeclaration)
				return (null, null);

			var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
			var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
			var syncedClassMembers = SourceGeneratorHelpers.GetAllMembers(typeSymbol, x => SyncHelpers.HasSyncAttribute(x)).ToArray();
			var hashCodeStrings = new List<string>();
			var syncsTargets = false;
			foreach (var member in syncedClassMembers)
			{
				var isTarget = false;
				if (member is IFieldSymbol field)
					hashCodeStrings.Add(SyncHelpers.GetHashCodeString(field.Type.Name, member.Name, out isTarget));

				if (member is IPropertySymbol property)
					hashCodeStrings.Add(SyncHelpers.GetHashCodeString(property.Type.Name, member.Name, out isTarget));

				syncsTargets |= isTarget;
			}

			var isSealed = classDeclaration.IsSealed();

			return GenerateClass(namespaceDeclaration.Name, classDeclaration.Identifier.Text, hashCodeStrings, isSealed, syncsTargets);
		}

		static (string FileName, string Content) GenerateClass(NameSyntax namespaceName, string className, IEnumerable<string> syncMembersAsHashCodeStrings, bool isSealed, bool syncsTargets)
		{
			return ($"{namespaceName}.{className}.g.cs",
				$@"// <auto-generated/>
{(syncsTargets ? "using OpenRA.Traits;\n" : string.Empty)}
namespace {namespaceName}
{{
	// Explicitly implement the interface again to avoid issues if it is inherited.
	{(isSealed ? "sealed" : "public")} partial class {className} : {SyncHelpers.SyncInterfaceName}
	{{
		int GetSyncHash()
		{{
			return {string.Join(" ^ ", syncMembersAsHashCodeStrings)};
		}}{(syncsTargets ? $"\n{SyncHelpers.GetTargetSyncHashMethod()}" : string.Empty)}
	}}
}}");
		}
	}
}
