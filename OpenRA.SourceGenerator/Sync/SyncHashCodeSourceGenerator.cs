using System;
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
				Console.WriteLine(syncedClassMembers);
			}
		}
	}
}
