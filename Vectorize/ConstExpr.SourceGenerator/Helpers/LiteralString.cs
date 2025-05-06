using System;

namespace ConstExpr.SourceGenerator.Helpers;

public readonly struct LiteralString(string value)
{
	public string Value { get; } = value;
	public bool IsEmpty => String.IsNullOrEmpty(Value);

	public static explicit operator LiteralString(string value) => new(value);
}