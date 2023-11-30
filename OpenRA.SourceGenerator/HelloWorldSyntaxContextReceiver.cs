using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.SourceGenerator
{
	class HelloWorldSyntaxContextReceiver : ISyntaxContextReceiver
	{
		public const string SyncInterfaceName = "OpenRA.ISync";

		public List<ClassDeclarationSyntax> ClassDeclarations = new();

		public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
		{
			if (context.Node is not ClassDeclarationSyntax classDeclarationSyntax || !IsPartial(classDeclarationSyntax))
				return;

			if (context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol && ImplementsISync(classSymbol))
				ClassDeclarations.Add(classDeclarationSyntax);
		}

		public static bool ImplementsISync(INamedTypeSymbol type) => type.AllInterfaces.Any(y => y.ToDisplayString() == SyncInterfaceName);

		public static bool IsPartial(ClassDeclarationSyntax classDeclaration)
			=> classDeclaration.Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
	}
}
