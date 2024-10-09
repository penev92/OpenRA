using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.SourceGenerators.Sync
{
	class SyncClass
	{
		private readonly string _namespace;
		private readonly string _name;
		private readonly string _namespace;

		public SyncClass(ClassDeclarationSyntax classDeclaration)
		{

		}

		public string GetCode()
		{

		}

		void Parse(ClassDeclarationSyntax classDeclaration)
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
			var (fileName, sourceCode) = GenerateClass(namespaceDeclaration.Name, classDeclaration.Identifier.Text, hashCodeStrings, isSealed, syncsTargets);
		}
	}
}
