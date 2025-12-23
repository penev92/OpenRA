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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.Roslyn.SourceGenerators.Sync
{
	// https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/
	[Generator]
	public class SyncHashCodeSourceGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			// This will filter only (all) partial classes with GenerateSyncCodeAttribute that don't explicitly implement ISync
			// then parse ALL OF THEM into a cunstom struct with all the data necessary to generate the sync code
			// and cache those.
			var syncableClassInfoProvider = context.SyntaxProvider
				.ForAttributeWithMetadataName( // TODO: Check if this fires up for inherited attributes. (we decided we want them explicit, but still should check!)
					SyncHelpers.FullyQualifiedGenerateSyncCodeAttributeName,
					predicate: (_, _) => true, //SourceGeneratorHelpers.IsPartialClass,  What if we let it try to generate on non-partial classes with the attribute as a way of telling the user they're doing something wrong?
					transform: GetSyncableClassInfo)
				.Where(static x => x.IsValid)
				.Select(static (x, _) => x.SyncableClassInfo);

			context.RegisterSourceOutput(syncableClassInfoProvider, Execute);

			//// TODO: Don't combine with Compilation!
			//var compilationAndClasses = context.CompilationProvider.Combine(syncableClassInfoProvider);

			//context.RegisterSourceOutput(compilationAndClasses,
			//	static (context, source) => ProcessClassDeclarations(source.Left, source.Right, context));
		}

		static (SyncableClassInfo SyncableClassInfo, bool IsValid) GetSyncableClassInfo(GeneratorAttributeSyntaxContext context, CancellationToken _)
		{
			var classSyntax = context.TargetNode as ClassDeclarationSyntax;
			var namespaceName = SourceGeneratorHelpers.GetNameSpace(context.TargetNode);
			var classModifiers = classSyntax.Modifiers;
			//var methodModifiers = ; make this a list of strings, then concat them.
			//var shouldCallBase = typeSymbol.BaseType.HasOrInheritsGenerateSyncCodeAttribute();
			return (default, false);
		}

		static void Execute(SourceProductionContext context, SyncableClassInfo classInfo)
		{
			var (filename, content) = GenerateClass(classInfo.NamespaceName, classDeclaration, typeSymbol, syncedClassMembers, shouldCallBase);
			//if (content != null)
			//	context.AddSource(filename, content);
		}

		// TODO: So this should return our own SyncableClassInfo, not *Syntax?!
		static ClassDeclarationSyntax GetClassDeclarationSymbol(GeneratorAttributeSyntaxContext context)
		{
			if (context.TargetSymbol is INamedTypeSymbol typeSymbol
				&& !typeSymbol.ManuallyImplementsISync())
				return context.TargetNode as ClassDeclarationSyntax;

			return null;
		}

		static void ProcessClassDeclarations(Compilation compilation, ClassDeclarationSyntax classDeclaration, SourceProductionContext context)
		{
			if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceDeclaration)
				return;

			var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
			var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
			if (typeSymbol == null)
				return;

			var syncedClassMembers = GetSyncHashElements(typeSymbol);
			if (!syncedClassMembers.Any())
				return;

			var shouldCallBase = typeSymbol.BaseType.HasOrInheritsGenerateSyncCodeAttribute();
			var (filename, content) = GenerateClass(namespaceDeclaration.Name, classDeclaration, typeSymbol, syncedClassMembers, shouldCallBase);
			if (content != null)
				context.AddSource(filename, content);
		}

		static IEnumerable<string> GetSyncHashElements(INamedTypeSymbol classSymbol)
		{
			// If not abstract, only care about your own members (and potentially call the base method later if needed).
			if (!classSymbol.IsAbstract)
				return classSymbol.GetMembers().Where(static x => x.HasVerifySyncAttribute()).Select(static x => x.Name);

			var symbol = classSymbol;
			var syncElements = new List<string>();

			while (symbol.IsAbstract && symbol.Name != "Object")
			{
				var members = symbol.GetMembers().Where(static x => x.HasVerifySyncAttribute());
				syncElements.AddRange(members.Select(static x => x.Name));
				symbol = symbol.BaseType;
			}

			return syncElements;
		}

		static (string FileName, string Content) GenerateClass(NameSyntax namespaceName,
			ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol, IEnumerable<string> syncMembers, bool shouldCallBase)
		{
			var className = classSymbol.Name;
			var isSealed = classSymbol.IsSealed;
			var elements = syncMembers.Select(static x => $"Sync.Hash({x})");
			if (shouldCallBase)
				elements = Enumerable.Append(elements, "base.GetSyncHash()");

			return ($"{namespaceName}.{className}.g.cs",
				$@"// <auto-generated/>
#pragma warning disable CS1591 // Apparently disabling in .editorconfig doesn't work for generated code.
namespace {namespaceName}
{{
	{classDeclaration.Modifiers} class {className} : {SyncHelpers.SyncInterfaceName}
	{{
		public {(shouldCallBase ? "override" : (isSealed ? "" : "virtual"))} int GetSyncHash()
		{{
			return {string.Join(" ^ ", elements)};
		}}
	}}
}}");
		}
	}
}
