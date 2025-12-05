using System.Collections.Generic;
using System.Diagnostics;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public delegate bool TryGetLiteralDelegate(SyntaxNode? expression, out object? value);

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
public sealed class BinaryOptimizeContext
{
	public BinaryOptimizeElement Left { get; init; }
	public BinaryOptimizeElement Right { get; init; }
	
	public Microsoft.CodeAnalysis.Operations.BinaryOperatorKind Kind { get; init; }

	public ITypeSymbol Type { get; init; }

	public IDictionary<string, VariableItem> Variables { get; init; }
	
	public TryGetLiteralDelegate TryGetLiteral { get; init; }

	internal string GetDebuggerDisplay()
	{
		var typeDisplay = Type?.ToDisplayString() ?? "null";
		var left = Left is null ? "null" : Left.GetDebuggerDisplay();
		var right = Right is null ? "null" : Right.GetDebuggerDisplay();
		return $"Type={typeDisplay} | Left=[{left}] | Right=[{right}]";
	}
}

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
public sealed class BinaryOptimizeElement
{
	public ExpressionSyntax Syntax { get; init; }
	public ITypeSymbol? Type { get; init; }
	public bool HasValue { get; init; }
	public object? Value { get; init; }

	internal string GetDebuggerDisplay()
	{
		var syntaxText = ShortSyntax();
		var typeText = Type?.ToDisplayString() ?? "null";
		var valueText = HasValue ? $"Value={FormatValue(Value)}" : "Value=<none>";
		return $"{syntaxText}";
	}

	private string ShortSyntax()
	{
		if (Syntax is null) return "Syntax=null";
		// Convert to single line and trim
		var s = Syntax.ToString().Replace("\r", " ").Replace("\n", " ").Trim();
		if (s.Length > 80) s = s.Substring(0, 77) + "...";
		return s; // .Length == 0 ? "Syntax=<empty>" : $"Syntax=\"{s}\"";
	}

	private static string FormatValue(object? v)
	{
		if (v is null) return "null";
		if (v is string s)
		{
			var display = s.Length > 60 ? s.Substring(0, 57) + "..." : s;
			return $"\"{display}\"";
		}
		try
		{
			var text = v.ToString() ?? v.GetType().Name;
			return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
		}
		catch
		{
			return $"<{v.GetType().Name}>";
		}
	}
}