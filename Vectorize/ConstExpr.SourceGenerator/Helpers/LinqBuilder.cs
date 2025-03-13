using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

public class LinqBuilder(Compilation compilation, ITypeSymbol elementType) : BaseBuilder(elementType)
{
	public void AppendAny(ITypeSymbol typeSymbol, IndentedStringBuilder builder)
	{
		if (!typeSymbol.CheckMethodWithReturnType("Any", compilation.GetSpecialType(SpecialType.System_Boolean), out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine("return true;");
		}
	}
	
	public void AppendToImmutableArray(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var immutableArrayType = SyntaxHelpers.GetTypeByType(compilation, typeof(ImmutableArray<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToImmutableArray", immutableArrayType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return ImmutableArray.Create({String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))});");
		}
	}

	public void AppendToArray(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var arrayType = compilation.CreateArrayTypeSymbol(elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToArray", arrayType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return [{String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))}];");
		}
	}

	public void AppendImmutableList(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var immutableListType = SyntaxHelpers.GetTypeByType(compilation, typeof(ImmutableList<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToImmutableList", immutableListType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return ImmutableList.Create({String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))});");
		}
	}

	public void AppendToList(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var listType = SyntaxHelpers.GetTypeByType(compilation, typeof(List<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToList", listType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return new List<{elementType.Name}>({String.Join(", ", items.Select(SyntaxHelpers.CreateLiteral))});");
		}
	}

	public void AppendToHashSet(ITypeSymbol typeSymbol, IList<object?> items, IndentedStringBuilder builder)
	{
		var hashSetType = SyntaxHelpers.GetTypeByType(compilation, typeof(HashSet<>), elementType);

		if (!typeSymbol.CheckMethodWithReturnType("ToHashSet", hashSetType, out var member))
		{
			return;
		}

		using (AppendMethod(builder, member))
		{
			builder.AppendLine($"return new HashSet<{elementType.Name}>({String.Join(", ", items.Distinct().Select(SyntaxHelpers.CreateLiteral))});");
		}
	}
}