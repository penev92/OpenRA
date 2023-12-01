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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenRA.SourceGenerators
{
	static class SourceGeneratorHelpers
	{
		public static bool IsPartialClass(this SyntaxNode node)
			=> node is ClassDeclarationSyntax classDeclaration
				&& classDeclaration.Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));

		public static bool IsSealed(this ClassDeclarationSyntax classDeclaration)
			=> classDeclaration.Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SealedKeyword));

		public static bool IsDerivedFromType(this INamedTypeSymbol symbol, string typeName)
		{
			if (symbol.Name == typeName)
				return true;

			if (symbol.BaseType == null)
				return false;

			return IsDerivedFromType(symbol.BaseType, typeName);
		}

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
