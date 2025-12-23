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

using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.Roslyn.SourceGenerators
{
	static class SourceGeneratorHelpers
	{
		public static bool IsPartialClass(SyntaxNode node, CancellationToken _)
			=> node is ClassDeclarationSyntax classDeclaration
				&& classDeclaration.Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));

		/// <summary>
		/// Determines the namespace the given <see cref="SyntaxNode"/> is declared in, if any.
		/// </summary>
		public static string GetNameSpace(SyntaxNode node)
		{
			var potentialNamespaceParent = node.Parent;
			while (potentialNamespaceParent != null
				   && potentialNamespaceParent is not BaseNamespaceDeclarationSyntax)
			{
				potentialNamespaceParent = potentialNamespaceParent.Parent;
			}

			return potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent
				? namespaceParent.Name.ToString()
				: string.Empty;
		}
	}
}
