using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
///   Strategy for collapsing an integer range check into a single unsigned comparison:
///   <c>x &gt;= a &amp;&amp; x &lt;= b</c> (a, b constants, a &lt;= b) → <c>(uint)(x - a) &lt;= (uint)(b - a)</c>.
///   The wrapping subtraction maps values below <c>a</c> to large unsigned values, so one
///   comparison covers both bounds. Exact for all inputs; safe under Strict.
/// </summary>
public class ConditionalAndUnsignedRangeCheckStrategy()
	: SymmetricStrategy<BooleanBinaryStrategy, BinaryExpressionSyntax, BinaryExpressionSyntax>(SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.LessThanOrEqualExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax.Left, context.Variables)
		    || context.TryGetValue(context.Left.Syntax.Left, out _) // fully constant: the literal strategies fold this
		    || !context.TryGetValue(context.Left.Syntax.Right, out var lowerValue)
		    || !context.TryGetValue(context.Right.Syntax.Right, out var upperValue)
		    || !context.Model.TryGetTypeSymbol(context.Left.Syntax.Left, context.SymbolStore, out var operandType))
		{
			return false;
		}

		var operand = context.Left.Syntax.Left;

		// ponytail: Int32/Int64 only; extend for the smaller/unsigned widths when needed.
		// MinValue lower bounds are excluded: that side is a tautology and negating MinValue overflows.
		if (operandType.SpecialType == SpecialType.System_Int32
		    && lowerValue is int intLower && upperValue is int intUpper
		    && intLower <= intUpper && intLower != Int32.MinValue)
		{
			return TryBuildRangeCheck(operand, intLower, unchecked((uint) intUpper - (uint) intLower), SyntaxKind.UIntKeyword, out optimized);
		}

		if (operandType.SpecialType == SpecialType.System_Int64
		    && lowerValue is long longLower && upperValue is long longUpper
		    && longLower <= longUpper && longLower != Int64.MinValue)
		{
			return TryBuildRangeCheck(operand, longLower, unchecked((ulong) longUpper - (ulong) longLower), SyntaxKind.ULongKeyword, out optimized);
		}

		return false;
	}

	private static bool TryBuildRangeCheck(ExpressionSyntax operand, long lower, object range, SyntaxKind castKeyword, out ExpressionSyntax? optimized)
	{
		optimized = null;

		ExpressionSyntax shifted;

		if (lower == 0)
		{
			shifted = operand;
		}
		else
		{
			var offset = lower > 0 ? lower : -lower;
			var offsetValue = castKeyword == SyntaxKind.UIntKeyword ? (object) (int) offset : offset;

			if (!TryCreateLiteral(offsetValue, out var offsetLiteral))
			{
				return false;
			}

			shifted = ParenthesizedExpression(lower > 0
				? SubtractExpression(operand, offsetLiteral)
				: AddExpression(operand, offsetLiteral));
		}

		if (!TryCreateLiteral(range, out var rangeLiteral))
		{
			return false;
		}

		var cast = CastExpression(PredefinedType(Token(castKeyword)), shifted);
		optimized = LessThanOrEqualExpression(cast, rangeLiteral);
		return true;
	}
}