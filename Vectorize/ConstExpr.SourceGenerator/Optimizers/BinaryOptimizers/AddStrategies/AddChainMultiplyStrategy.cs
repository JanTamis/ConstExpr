using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for add-chain deduplication: x + y + x + x => x * 3 + y
/// Flattens the entire addition chain, groups structurally equivalent operands,
/// and replaces repeated terms with a multiplication.
/// Requires AssociativeMath for floating-point safety (reordering operands).
/// For integers, reordering is always safe but we conservatively require it here
/// to avoid conflicting with AddDoubleToShiftStrategy (x + x => x &lt;&lt; 1).
/// </summary>
public class AddChainMultiplyStrategy : NumericBinaryStrategy
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimize(
		BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context,
		out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || context.Parent is BinaryExpressionSyntax parentBinary
		    && parentBinary.IsKind(SyntaxKind.AddExpression))
		{
			return false;
		}

		var operands = FlattenAddChain(context.Left.Syntax).Concat(FlattenAddChain(context.Right.Syntax)).ToList();

		var groups = operands
			.GroupBy(e => e, SyntaxNodeComparer.Get<ExpressionSyntax>())
			.Where(g => g.Count() > 1)
			.ToList();

		if (groups.Count == 0)
		{
			return false;
		}

		// Track which operands have been consumed into a multiply
		var consumed = new HashSet<int>();
		var terms = new List<ExpressionSyntax>();

		foreach (var group in groups)
		{
			var count = group.Count();
			var expr = group.Key.WithoutTrivia();

			// Mark indices as consumed
			var found = 0;

			for (var i = 0; i < operands.Count && found < count; i++)
			{
				if (!consumed.Contains(i)
				    && SyntaxNodeComparer.Get<ExpressionSyntax>().Equals(operands[i], expr))
				{
					consumed.Add(i);
					found++;
				}
			}

			if (expr is ConditionalExpressionSyntax conditionalExpression)
			{
				var result = conditionalExpression;
				var countLiteral = CreateLiteral(count.ToSpecialType(context.Type.SpecialType));
				var whenTrueIsLiteral = conditionalExpression.WhenTrue is LiteralExpressionSyntax;
				var whenFalseIsLiteral = conditionalExpression.WhenFalse is LiteralExpressionSyntax;

				if (whenTrueIsLiteral || whenFalseIsLiteral)
				{
					if (whenTrueIsLiteral)
					{
						var whenTrue = (LiteralExpressionSyntax) conditionalExpression.WhenTrue;
						result = result.WithWhenTrue(CreateLiteral(whenTrue.Token.Value.Multiply(count.ToSpecialType(context.Type.SpecialType))));
					}
					else
					{
						// WhenFalse is literal; multiply the non-literal WhenTrue explicitly.
						result = result.WithWhenTrue(MultiplyExpression(WrapForMultiply(conditionalExpression.WhenTrue), countLiteral));
					}

					if (whenFalseIsLiteral)
					{
						var whenFalse = (LiteralExpressionSyntax) conditionalExpression.WhenFalse;
						result = result.WithWhenFalse(CreateLiteral(whenFalse.Token.Value.Multiply(count.ToSpecialType(context.Type.SpecialType))));
					}
					else
					{
						// WhenTrue is literal; multiply the non-literal WhenFalse explicitly.
						result = result.WithWhenFalse(MultiplyExpression(WrapForMultiply(conditionalExpression.WhenFalse), countLiteral));
					}

					terms.Add(WrapForMultiply(result));
					continue;
				}
			}

			terms.Add(MultiplyExpression(WrapForMultiply(expr),
				CreateLiteral(count.ToSpecialType(context.Type.SpecialType))));
		}

		// Append remaining non-duplicated operands.
		// They are placed as operands of an AddExpression, so conditional expressions
		// and other low-precedence forms must be parenthesized to avoid mis-parsing.
		// e.g. "expr * 6 + cond ? 1 : 0" would parse as "(expr * 6 + cond) ? 1 : 0".
		for (var i = 0; i < operands.Count; i++)
		{
			if (!consumed.Contains(i))
			{
				terms.Add(WrapForAdd(operands[i]));
			}
		}

		optimized = terms.Count == 1
			? terms[0]
			: terms.Aggregate(AddExpression);

		var comparer = SyntaxNodeComparer.Get<ExpressionSyntax>();

		return !comparer.Equals(optimized, AddExpression(context.Left.Syntax, context.Right.Syntax));
	}

	/// <summary>
	/// Wraps <paramref name="expr"/> in parentheses when its operator precedence is lower than
	/// addition, so that it can safely appear as an operand of an <c>AddExpression</c>.
	/// For example, <c>x ? 1 : 0</c> becomes <c>(x ? 1 : 0)</c>, preventing the incorrect
	/// output <c>a * n + x ? 1 : 0</c> from being parsed as <c>(a * n + x) ? 1 : 0</c>.
	/// </summary>
	private static ExpressionSyntax WrapForAdd(ExpressionSyntax expr)
	{
		return expr switch
		{
			// High-precedence / already-safe expressions — no wrapping needed.
			LiteralExpressionSyntax
				or IdentifierNameSyntax
				or MemberAccessExpressionSyntax
				or InvocationExpressionSyntax
				or ElementAccessExpressionSyntax
				or PrefixUnaryExpressionSyntax
				or PostfixUnaryExpressionSyntax
				or ParenthesizedExpressionSyntax
				or CastExpressionSyntax
				or ObjectCreationExpressionSyntax
				or ImplicitObjectCreationExpressionSyntax
				or DefaultExpressionSyntax
				or TypeOfExpressionSyntax => expr,
			// Additive and higher-precedence binary expressions are safe.
			BinaryExpressionSyntax binary when binary.Kind() is
				SyntaxKind.MultiplyExpression or
				SyntaxKind.DivideExpression or
				SyntaxKind.ModuloExpression or
				SyntaxKind.AddExpression or
				SyntaxKind.SubtractExpression => expr,
			// Everything else (shift, relational, logical, ternary, …) is lower precedence.
			_ => ParenthesizedExpression(expr)
		};
	}

	/// <summary>
	/// Wraps <paramref name="expr"/> in parentheses when its operator precedence is lower than
	/// multiplication, so that <c>MultiplyExpression(expr, n)</c> produces correct syntax.
	/// For example, <c>x ? 1 : 0</c> becomes <c>(x ? 1 : 0)</c>, preventing the incorrect
	/// output <c>x ? 1 : 0 * n</c>.
	/// </summary>
	private static ExpressionSyntax WrapForMultiply(ExpressionSyntax expr)
	{
		return expr switch
		{
			// High-precedence / already-safe expressions — no wrapping needed.
			LiteralExpressionSyntax
				or IdentifierNameSyntax
				or MemberAccessExpressionSyntax
				or InvocationExpressionSyntax
				or ElementAccessExpressionSyntax
				or PrefixUnaryExpressionSyntax
				or PostfixUnaryExpressionSyntax
				or ParenthesizedExpressionSyntax
				or CastExpressionSyntax
				or ObjectCreationExpressionSyntax
				or ImplicitObjectCreationExpressionSyntax
				or DefaultExpressionSyntax
				or TypeOfExpressionSyntax => expr,
			// Multiplicative binary expressions are at the same precedence level — safe.
			BinaryExpressionSyntax binary when binary.Kind() is
				SyntaxKind.MultiplyExpression or
				SyntaxKind.DivideExpression or
				SyntaxKind.ModuloExpression => expr,
			// Everything else (additive, shift, relational, logical, ternary, …) is lower precedence.
			_ => ParenthesizedExpression(expr)
		};
	}

	private static IEnumerable<ExpressionSyntax> FlattenAddChain(ExpressionSyntax expr)
	{
		switch (expr)
		{
			case BinaryExpressionSyntax binary:
			{
				switch (binary.Kind())
				{
					case SyntaxKind.AddExpression:
					{
						return FlattenAddChain(binary.Left).Concat(FlattenAddChain(binary.Right));
					}
					case SyntaxKind.MultiplyExpression when binary.Right is LiteralExpressionSyntax { Token.Value: int n and > 1 }:
					{
						// Unwrap parentheses so that `(expr) * n` and a bare `expr` term are grouped together.
						var inner = binary.Left is ParenthesizedExpressionSyntax paren ? paren.Expression : binary.Left;
						return Enumerable.Repeat(inner, n);
					}
					case SyntaxKind.LeftShiftExpression when binary.Right is LiteralExpressionSyntax { Token.Value: int shiftN }:
					{
						return Enumerable.Repeat(binary.Left, shiftN * 2);
					}
				}
				break;
			}
			case ParenthesizedExpressionSyntax paren:
			{
				return FlattenAddChain(paren.Expression);
			}
		}

		return [ expr ];
	}
}