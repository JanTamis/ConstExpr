using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Models;

public class VariableItem(ITypeSymbol type, bool hasValue, object? value, bool canBeNull, bool isInitialized = false)
{
	public ITypeSymbol Type { get; set; } = type;

	public object? Value { get; set; } = value;

	public bool HasValue { get; set; } = hasValue;

	public bool IsInitialized { get; set; } = isInitialized;

	public bool IsAccessed { get; set; }

	public bool IsAltered { get; set; }

	public bool CanBeInlined { get; set; }

	public bool CanBeNull { get; set; } = canBeNull;

	/// <summary>
	///   Indices of an array/indexable value that hold a runtime (non-constant) value. Other elements
	///   remain foldable. Lets a non-constant write to one element avoid invalidating the whole value
	///   (the coarse <see cref="IsAltered" /> hammer).
	/// </summary>
	public HashSet<int>? UnknownIndices { get; set; }

	/// <summary>
	///   For an inlined local (<see cref="CanBeInlined" />) that merely aliases another tracked buffer
	///   (<c>var a = arr;</c>): the name of that buffer's variable. The declaration of <c>a</c> is removed, so
	///   an element write through it (<c>a[0] = 9;</c>) has to be applied to - and spelled through - the
	///   aliased variable, or the write never reaches the tracked value and <c>a</c> itself is undeclared.
	/// </summary>
	public string? BufferAlias { get; set; }

	public bool HasUnknownElements => UnknownIndices is { Count: > 0 };
}