using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SourceGen.Utilities.Extensions;

public static class CompilerExtensions
{
	public static string GetMinimalString(this Compilation compilation, ISymbol? symbol)
	{
		switch (symbol)
		{
			case null:
				return String.Empty;
			// Check for built-in type keywords
			case ITypeSymbol typeSymbol:
				switch (typeSymbol.SpecialType)
				{
					case SpecialType.System_Void: return "void";
					case SpecialType.System_Boolean: return "bool";
					case SpecialType.System_Byte: return "byte";
					case SpecialType.System_SByte: return "sbyte";
					case SpecialType.System_Int16: return "short";
					case SpecialType.System_UInt16: return "ushort";
					case SpecialType.System_Int32: return "int";
					case SpecialType.System_UInt32: return "uint";
					case SpecialType.System_Int64: return "long";
					case SpecialType.System_UInt64: return "ulong";
					case SpecialType.System_Single: return "float";
					case SpecialType.System_Double: return "double";
					case SpecialType.System_Decimal: return "decimal";
					case SpecialType.System_Char: return "char";
					case SpecialType.System_String: return "string";
					case SpecialType.System_Object: return "object";
				}
				break;
		}

		if (compilation.IsSpecialType(symbol.OriginalDefinition, SpecialType.System_Collections_Generic_IEnumerable_T)
		    && symbol is INamedTypeSymbol namedTypeSymbol)
		{
			return $"IEnumerable<{String.Join(", ", namedTypeSymbol.TypeArguments.Select(s => GetMinimalString(compilation, s)))}>";
		}

		switch (symbol)
		{
			case IArrayTypeSymbol arrayTypeSymbol:
			{
				var elementType = GetMinimalString(compilation, arrayTypeSymbol.ElementType);

				return $"{elementType}[{new string(',', arrayTypeSymbol.Rank - 1)}]";
			}
			case INamedTypeSymbol { Arity: > 0, IsTupleType: true } namedSymbol:
			{
				return $"({String.Join(", ", namedSymbol.TypeArguments.Select(compilation.GetMinimalString))})";
			}
			case INamedTypeSymbol { Arity: > 0 } namedSymbol:
			{
				return $"{namedSymbol.Name}<{String.Join(", ", namedSymbol.TypeArguments.Select(s => GetMinimalString(compilation, s)))}>";
			}
		}

		var node = GetSyntaxNode(symbol);

		if (node is null || !compilation.TryGetSemanticModel(node, out var model))
		{
			return symbol.Name;
		}

		return symbol.ToMinimalDisplayString(model, node.Span.Start);

		static SyntaxNode? GetSyntaxNode(ISymbol symbol)
		{
			return symbol.DeclaringSyntaxReferences
				.Select(s => s.GetSyntax())
				.FirstOrDefault(s => s is not null);
		}
	}

	public static bool IsSpecialType(this Compilation compilation, ISymbol symbol, SpecialType specialType)
	{
		if (symbol is ITypeSymbol namedTypeSymbol)
		{
			return namedTypeSymbol.SpecialType == specialType;
		}

		return SymbolEqualityComparer.Default.Equals(symbol, compilation.GetSpecialType(specialType));
	}

	public static bool TryGetSemanticModel(this Compilation compilation, SyntaxNode? node, out SemanticModel semanticModel)
	{
		var tree = node?.SyntaxTree;

		if (tree is not null && compilation.SyntaxTrees.Contains(tree))
		{
			semanticModel = compilation.GetSemanticModel(tree);
			return true;
		}

		semanticModel = null!;
		return false;
	}

	public static bool TryGetSemanticModel(this Compilation compilation, ISymbol? symbol, out SemanticModel semanticModel)
	{
		var tree =  symbol?.DeclaringSyntaxReferences
			.Select(s => s.SyntaxTree)
			.FirstOrDefault();

		if (tree is not null && compilation.SyntaxTrees.Contains(tree))
		{
			semanticModel = compilation.GetSemanticModel(tree);
			return true;
		}

		semanticModel = null!;
		return false;
	}

	public static bool TryGetValue(this Compilation compilation, ISymbol symbol, out Optional<object?> value)
	{
		if (TryGetSemanticModel(compilation, symbol, out var model))
		{
			value = symbol.DeclaringSyntaxReferences
				.Select(s => model.GetConstantValue(s.GetSyntax()))
				.FirstOrDefault(f=> f.HasValue);
			
			return value.HasValue;
		}
		
		value = default;
		return false;
	}
	
	public static bool TryGetOperation(this Compilation compilation, ISymbol symbol, out IOperation? operation)
	{
		if (TryGetSemanticModel(compilation, symbol, out var model))
		{
			operation = symbol.DeclaringSyntaxReferences
				.Select(s => s.GetSyntax())
				.SelectMany(n => new[] { model.GetOperation(n) }.Concat(n.DescendantNodes().Select(s => model.GetOperation(s))))
				.FirstOrDefault(op => op is not null);
			
			return operation is not null;
		}
		
		operation = null;
		return false;
	}

	public static bool IsPrimitiveType(this ITypeSymbol typeSymbol)
	{
		return typeSymbol.SpecialType is SpecialType.System_Boolean
			or SpecialType.System_Byte
			or SpecialType.System_SByte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64
			or SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Decimal
			or SpecialType.System_Char;

	}
}