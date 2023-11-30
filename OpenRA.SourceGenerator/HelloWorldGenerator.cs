using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OpenRA.SourceGenerator;

namespace OpenRA.SourceGenerators
{
	[Generator]
	public class HelloWorldGenerator : ISourceGenerator
	{
		public const string SyncAttributeName = "SyncAttribute";

		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new HelloWorldSyntaxContextReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{

			foreach (var classDeclaration in ((HelloWorldSyntaxContextReceiver)context.SyntaxContextReceiver).ClassDeclarations)
			{
				if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceDeclaration)
					return;

				var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
				var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
				var syncedClassMembers = GetAllMembers(typeSymbol, x => HasSyncAttribute(x)).ToArray();
				var hashCodeStrings = new List<string>();
				var syncsTargets = false;
				//foreach (var member in syncedClassMembers)
				//{
				//	var isTarget = false;
				//	if (member is IFieldSymbol field)
				//		hashCodeStrings.Add(SyncHelpers.GetHashCodeString(field.Type.Name, member.Name, out isTarget));

				//	if (member is IPropertySymbol property)
				//		hashCodeStrings.Add(SyncHelpers.GetHashCodeString(property.Type.Name, member.Name, out isTarget));

				//	syncsTargets |= isTarget;
				//}

				//var isSealed = classDeclaration.IsSealed();
				//var sourceCode = GenerateClassCode(namespaceDeclaration.Name, classDeclaration.Identifier.Text, hashCodeStrings, isSealed, syncsTargets);
				//context.AddSource($"{classDeclaration.Identifier.Text}.g.cs", sourceCode);
			}


			// begin creating the source we'll inject into the users compilation
			var sourceCode = SourceText.From(@"
#pragma warning disable 1591
namespace OpenRA {
public static class GeneratedCode
{
    public static string GeneratedMessage = ""Hello from Generated Code"";
}}
#pragma warning restore 1591 
", Encoding.UTF8);

			context.AddSource("GeneratedCode.g.cs", sourceCode);
		}

		public static bool HasSyncAttribute(ISymbol symbol) => symbol.GetAttributes().Any(x => x.AttributeClass.Name == SyncAttributeName);

		public static IEnumerable<ISymbol> GetAllMembers(ITypeSymbol typeSymbol, Func<ISymbol, bool> predicate)
		{
			foreach (var symbol in typeSymbol.GetMembers().Where(predicate))
				yield return symbol;

			if (typeSymbol.BaseType == null)
				yield break;

			foreach (var symbol in GetAllMembers(typeSymbol.BaseType, predicate))
				yield return symbol;
		}
	}
}
