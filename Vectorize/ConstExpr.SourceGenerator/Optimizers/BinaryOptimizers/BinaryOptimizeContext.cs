using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public delegate bool TryGetLiteralDelegate(SyntaxNode? expression, [NotNullWhen(true)] out object? value);

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
public sealed class BinaryOptimizeContext<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public BinaryOptimizeElement<TLeft> Left { get; init; }
	public BinaryOptimizeElement<TRight> Right { get; init; }

	// public BinaryOperatorKind Kind { get; init; }

	public ITypeSymbol Type { get; init; }

	// public IDictionary<string, VariableItem> Variables { get; init; }

	public TryGetLiteralDelegate TryGetLiteral { get; init; }

	public IList<BinaryExpressionSyntax> BinaryExpressions { get; init; }

	internal string GetDebuggerDisplay()
	{
		var typeDisplay = Type?.ToDisplayString() ?? "null";
		var left = Left is null ? "null" : Left.GetShortSyntax();
		var right = Right is null ? "null" : Right.GetShortSyntax();

		return $"Type={typeDisplay} | Left=[{left}] | Right=[{right}]";
	}
}

[DebuggerDisplay("{GetShortSyntax(),nq}")]
public sealed class BinaryOptimizeElement<T>
	where T : ExpressionSyntax
{
	public T Syntax { get; init; }
	public ITypeSymbol? Type { get; init; }

	internal string GetShortSyntax()
	{
		if (Syntax is null)
		{
			return "Syntax=null";
		}

		// Convert to single line and trim
		var s = Syntax.ToString().Replace("\r", " ").Replace("\n", " ").Trim();

		if (s.Length > 80)
		{
			s = s.Substring(0, 77) + "...";
		}

		return s; // .Length == 0 ? "Syntax=<empty>" : $"Syntax=\"{s}\"";
	}
}

