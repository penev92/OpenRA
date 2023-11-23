using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.SourceGenerators.Sync
{
	/// <summary>
	/// Basically this goes over every syntax node in the codebase and goes
	/// "Is this a class that is partial and also implements ISync? That's the one I'm after!".
	/// </summary>
	class SyncHashCodeSyntaxContextReceiver : ISyntaxContextReceiver
	{
		public List<ClassDeclarationSyntax> ClassDeclarations = new();

		public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
		{
			if (context.Node is not ClassDeclarationSyntax classDeclarationSyntax || !classDeclarationSyntax.IsPartial())
				return;

			if (context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol && classSymbol.ImplementsISync())
				ClassDeclarations.Add(classDeclarationSyntax);
		}
	}
}
