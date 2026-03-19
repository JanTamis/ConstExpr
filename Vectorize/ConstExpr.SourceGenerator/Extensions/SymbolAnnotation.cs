using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Extensions;

/// <summary>
/// Provides methods to annotate syntax nodes with symbol information that persists
/// beyond the original semantic model. This is used for synthetic/optimized nodes
/// created by LINQ optimizers, which are not part of the original syntax tree and
/// therefore cannot be resolved by the <see cref="SemanticModel"/>.
/// </summary>
public static class SymbolAnnotation
{
	private const string MethodSymbolKind = "ConstExpr_MethodSymbol";
	private const string TypeSymbolKind = "ConstExpr_TypeSymbol";

	private static readonly ConcurrentDictionary<string, ISymbol> SymbolStore = new();

	/// <summary>
	/// Annotates a syntax node with an <see cref="IMethodSymbol"/>.
	/// Returns a new node with the annotation attached.
	/// </summary>
	public static T WithMethodSymbolAnnotation<T>(this T node, IMethodSymbol symbol) where T : SyntaxNode
	{
		var id = Guid.NewGuid().ToString("N");
		SymbolStore[id] = symbol;
		
		return node.WithAdditionalAnnotations(new SyntaxAnnotation(MethodSymbolKind, id));
	}

	/// <summary>
	/// Annotates a syntax node with an <see cref="ITypeSymbol"/>.
	/// Returns a new node with the annotation attached.
	/// </summary>
	public static T WithTypeSymbolAnnotation<T>(this T node, ITypeSymbol symbol) where T : SyntaxNode
	{
		var id = Guid.NewGuid().ToString("N");
		SymbolStore[id] = symbol;
		return node.WithAdditionalAnnotations(new SyntaxAnnotation(TypeSymbolKind, id));
	}

	/// <summary>
	/// Tries to retrieve an annotated <see cref="IMethodSymbol"/> from a syntax node.
	/// </summary>
	public static bool TryGetMethodSymbolAnnotation(this SyntaxNode? node, [NotNullWhen(true)] out IMethodSymbol? symbol)
	{
		symbol = null;

		if (node is null)
		{
			return false;
		}

		var annotation = node.GetAnnotations(MethodSymbolKind).FirstOrDefault();

		if (annotation?.Data is not null
		    && SymbolStore.TryGetValue(annotation.Data, out var s)
		    && s is IMethodSymbol method)
		{
			symbol = method;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Tries to retrieve an annotated <see cref="ITypeSymbol"/> from a syntax node.
	/// </summary>
	public static bool TryGetTypeSymbolAnnotation(this SyntaxNode? node, [NotNullWhen(true)] out ITypeSymbol? symbol)
	{
		symbol = null;

		if (node is null)
		{
			return false;
		}

		var annotation = node.GetAnnotations(TypeSymbolKind).FirstOrDefault();

		if (annotation?.Data is not null
		    && SymbolStore.TryGetValue(annotation.Data, out var s)
		    && s is ITypeSymbol type)
		{
			symbol = type;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Clears the symbol annotation store. Should be called at the end of a generation pass
	/// to prevent memory leaks from accumulated symbol references.
	/// </summary>
	public static void Clear()
	{
		SymbolStore.Clear();
	}
}

