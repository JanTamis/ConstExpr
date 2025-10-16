using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Visitors;

public class VariableItem(ITypeSymbol type, bool hasValue, object? value, bool isInitialized = false)
{
	public ITypeSymbol Type { get; } = type;

	public object? Value { get; set; } = value;

	public bool HasValue { get; set; } = hasValue;

	public bool IsInitialized { get; set; } = isInitialized;

	public object? MinValue { get; set; }
	public object? MaxValue { get; set; }
}