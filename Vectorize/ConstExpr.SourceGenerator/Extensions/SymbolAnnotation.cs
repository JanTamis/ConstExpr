using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Extensions;

/// <summary>
///   Provides methods to annotate syntax nodes with symbol information that persists
///   beyond the original semantic model. This is used for synthetic/optimized nodes
///   created by LINQ optimizers, which are not part of the original syntax tree and
///   therefore cannot be resolved by the <see cref="SemanticModel" />.
/// </summary>
public static class SymbolAnnotation
{
	private const string MethodSymbolKind = "ConstExpr_MethodSymbol";
	private const string TypeSymbolKind = "ConstExpr_TypeSymbol";
	private const string GeneralSymbolKind = "ConstExpr_GeneralSymbol";
	private const string LookupCountValueKind = "ConstExpr_LookupCount";

	// private static readonly ConcurrentDictionary<ulong, ISymbol> SymbolStore = new();

	/// <summary>
	///   Annotates a syntax node with an <see cref="IMethodSymbol" />.
	///   Returns a new node with the annotation attached.
	/// </summary>
	public static T WithMethodSymbolAnnotation<T>(this T node, IMethodSymbol? symbol, ConcurrentDictionary<ulong, ISymbol> symbolStore) where T : SyntaxNode
	{
		if (symbol is null)
		{
			return node;
		}

		var id = DeteministicHashVisitor.Instance.Visit(node);
		symbolStore[id] = symbol;
		return node.WithAdditionalAnnotations(new SyntaxAnnotation(MethodSymbolKind, id.ToString()));
	}

	/// <summary>
	///   Annotates a syntax node with an <see cref="ITypeSymbol" />.
	///   Returns a new node with the annotation attached.
	/// </summary>
	public static T? WithTypeSymbolAnnotation<T>(this T node, ITypeSymbol? symbol, ConcurrentDictionary<ulong, ISymbol> symbolStore) where T : SyntaxNode
	{
		if (symbol is null)
		{
			return node;
		}

		var id = DeteministicHashVisitor.Instance.Visit(node);
		symbolStore[id] = symbol;
		return node?.WithAdditionalAnnotations(new SyntaxAnnotation(TypeSymbolKind, id.ToString()));
	}

	/// <summary>
	///   Tries to retrieve an annotated <see cref="IMethodSymbol" /> from a syntax node.
	/// </summary>
	public static bool TryGetMethodSymbolAnnotation(this SyntaxNode? node, ConcurrentDictionary<ulong, ISymbol> symbolStore, [NotNullWhen(true)] out IMethodSymbol? symbol)
	{
		symbol = null;

		if (node is null)
		{
			return false;
		}

		var annotation = node.GetAnnotations(MethodSymbolKind).FirstOrDefault();

		if (annotation?.Data is not null
		    && symbolStore.TryGetValue(UInt64.Parse(annotation.Data), out var s)
		    && s is IMethodSymbol method)
		{
			symbol = method;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Tries to retrieve an annotated <see cref="ITypeSymbol" /> from a syntax node.
	/// </summary>
	public static bool TryGetTypeSymbolAnnotation(this SyntaxNode? node, ConcurrentDictionary<ulong, ISymbol> symbolStore, [NotNullWhen(true)] out ITypeSymbol? symbol)
	{
		symbol = null;

		if (node is null)
		{
			return false;
		}

		var annotation = node.GetAnnotations(TypeSymbolKind).FirstOrDefault();

		if (annotation?.Data is not null
		    && symbolStore.TryGetValue(UInt64.Parse(annotation.Data), out var s)
		    && s is ITypeSymbol type)
		{
			symbol = type;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Annotates a syntax node with any <see cref="ISymbol" /> (e.g. an <see cref="IPropertySymbol" />) -
	///   the method/type variants above only cover those two specific symbol kinds.
	///   Returns a new node with the annotation attached.
	/// </summary>
	public static T WithSymbolAnnotation<T>(this T node, ISymbol? symbol, ConcurrentDictionary<ulong, ISymbol> symbolStore) where T : SyntaxNode
	{
		if (symbol is null)
		{
			return node;
		}

		var id = DeteministicHashVisitor.Instance.Visit(node);
		symbolStore[id] = symbol;
		return node.WithAdditionalAnnotations(new SyntaxAnnotation(GeneralSymbolKind, id.ToString()));
	}

	/// <summary>
	///   Tries to retrieve an annotated <see cref="ISymbol" /> from a syntax node (see <see cref="WithSymbolAnnotation{T}" />).
	/// </summary>
	public static bool TryGetSymbolAnnotation(this SyntaxNode? node, ConcurrentDictionary<ulong, ISymbol> symbolStore, [NotNullWhen(true)] out ISymbol? symbol)
	{
		symbol = null;

		if (node is null)
		{
			return false;
		}

		var annotation = node.GetAnnotations(GeneralSymbolKind).FirstOrDefault();

		if (annotation?.Data is not null && symbolStore.TryGetValue(UInt64.Parse(annotation.Data), out var s))
		{
			symbol = s;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Annotates an `ObjectCreationExpressionSyntax` for a compile-time-generated `ILookup` struct
	///   (see ToLookupFunctionOptimizer) with its known group count. The struct's `Count` property can
	///   never be resolved through the semantic model or reflection - it is source this same generator
	///   pass is emitting, so it has no compiled metadata to reflect over yet. Stashing the value directly
	///   on the node lets a later `receiver.Count` read recover it without needing either.
	/// </summary>
	public static T WithLookupCountAnnotation<T>(this T node, int count) where T : SyntaxNode
	{
		return node.WithAdditionalAnnotations(new SyntaxAnnotation(LookupCountValueKind, count.ToString()));
	}

	/// <summary>
	///   Tries to retrieve a group count annotated via <see cref="WithLookupCountAnnotation{T}" />.
	/// </summary>
	public static bool TryGetLookupCountAnnotation(this SyntaxNode? node, out int count)
	{
		count = 0;

		var annotation = node?.GetAnnotations(LookupCountValueKind).FirstOrDefault();

		return annotation?.Data is { } data && Int32.TryParse(data, out count);
	}
}