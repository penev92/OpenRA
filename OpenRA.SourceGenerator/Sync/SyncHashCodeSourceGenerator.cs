using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OpenRA.SourceGenerators.Sync
{
	[Generator]
	public class SyncHashCodeSourceGenerator : ISourceGenerator
	{
		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new SyncHashCodeSyntaxContextReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{
			var sourceCode2 = SourceText.From(@"
#pragma warning disable 1591
namespace OpenRA {
public static class GeneratedCode
{
    public static string GeneratedMessage = ""Hello from Generated Code"";
}}
#pragma warning restore 1591 
", Encoding.UTF8);

			context.AddSource("GeneratedCode.g.cs", sourceCode2);

			foreach (var classDeclaration in ((SyncHashCodeSyntaxContextReceiver)context.SyntaxContextReceiver).ClassDeclarations)
			{
				if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceDeclaration)
					return;

				var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
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
				var className = classDeclaration.Identifier.Text;
				var sourceCode = GenerateClassCode("OpenRA.Traits", className, hashCodeStrings, isSealed, syncsTargets);
				context.AddSource(className + ".g.cs", sourceCode);
				break;
			}
		}

		SourceText GenerateClassCode(string namespaceName, string className, IEnumerable<string> syncMembersAsHashCodeStrings, bool isSealed, bool syncsTargets)
		{
			Console.WriteLine(namespaceName);
			Console.WriteLine(className);
			Console.WriteLine(string.Join(", ", syncMembersAsHashCodeStrings));
			Console.WriteLine(isSealed);
			Console.WriteLine(syncsTargets);
			return SourceText.From(@"
#pragma warning disable 1591
namespace OpenRA {
public static class GeneratedCode2
{
    public static string GeneratedMessage = ""Hello from Generated Code"";
}}
#pragma warning restore 1591 
", Encoding.UTF8);
		}
	}
}
