using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Models;

public class VariableItem(ITypeSymbol type, bool hasValue, object? value, bool isInitialized = false)
{
	public ITypeSymbol Type { get; set; } = type;

	public object? Value { get; set; } = value;

	public bool HasValue { get; set; } = hasValue;

	public bool IsInitialized { get; set; } = isInitialized;

	public bool IsAccessed { get; set; }

	public bool IsAltered { get; set; }

	public bool CanBeInlined { get; set; }

	/// <summary>
	///   Indices of an array/indexable value that hold a runtime (non-constant) value. Other elements
	///   remain foldable. Lets a non-constant write to one element avoid invalidating the whole value
	///   (the coarse <see cref="IsAltered" /> hammer).
	/// </summary>
	public HashSet<int>? UnknownIndices { get; set; }

	public bool HasUnknownElements => UnknownIndices is { Count: > 0 };
}