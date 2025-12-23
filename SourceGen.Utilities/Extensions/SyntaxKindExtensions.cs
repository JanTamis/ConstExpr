using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace SourceGen.Utilities.Extensions;

public static class SyntaxKindExtensions
{
	public static BinaryOperatorKind ToBinaryOperatorKind(this SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.PlusToken or SyntaxKind.AddExpression => BinaryOperatorKind.Add,
			SyntaxKind.MinusToken or SyntaxKind.SubtractExpression => BinaryOperatorKind.Subtract,
			SyntaxKind.AsteriskToken or SyntaxKind.MultiplyExpression => BinaryOperatorKind.Multiply,
			SyntaxKind.SlashToken or SyntaxKind.DivideExpression => BinaryOperatorKind.Divide,
			SyntaxKind.PercentToken or SyntaxKind.ModuloExpression => BinaryOperatorKind.Remainder,
			SyntaxKind.LessThanLessThanToken or SyntaxKind.LeftShiftExpression => BinaryOperatorKind.LeftShift,
			SyntaxKind.GreaterThanGreaterThanToken or SyntaxKind.RightShiftExpression => BinaryOperatorKind.RightShift,
			SyntaxKind.GreaterThanGreaterThanGreaterThanToken or SyntaxKind.UnsignedRightShiftExpression => BinaryOperatorKind.UnsignedRightShift,
			SyntaxKind.AmpersandToken or SyntaxKind.BitwiseAndExpression => BinaryOperatorKind.And,
			SyntaxKind.BarToken or SyntaxKind.BitwiseOrExpression => BinaryOperatorKind.Or,
			SyntaxKind.CaretToken or SyntaxKind.ExclusiveOrExpression => BinaryOperatorKind.ExclusiveOr,
			SyntaxKind.AmpersandAmpersandToken or SyntaxKind.LogicalAndExpression => BinaryOperatorKind.ConditionalAnd,
			SyntaxKind.BarBarToken or SyntaxKind.LogicalOrExpression => BinaryOperatorKind.ConditionalOr,
			SyntaxKind.LessThanToken or SyntaxKind.LessThanExpression => BinaryOperatorKind.LessThan,
			SyntaxKind.LessThanEqualsToken or SyntaxKind.LessThanOrEqualExpression => BinaryOperatorKind.LessThanOrEqual,
			SyntaxKind.GreaterThanToken or SyntaxKind.GreaterThanExpression => BinaryOperatorKind.GreaterThan,
			SyntaxKind.GreaterThanEqualsToken or SyntaxKind.GreaterThanOrEqualExpression => BinaryOperatorKind.GreaterThanOrEqual,
			SyntaxKind.EqualsEqualsToken or SyntaxKind.EqualsExpression => BinaryOperatorKind.Equals,
			SyntaxKind.ExclamationEqualsToken or SyntaxKind.NotEqualsExpression => BinaryOperatorKind.NotEquals,
			_ => throw new ArgumentException($"Unknown binary operator: {kind}", nameof(kind))
		};
	}
	
	public static bool IsKind(this CSharpSyntaxNode node, params ReadOnlySpan<SyntaxKind> kinds)
	{
		var nodeKind = node.Kind();

		foreach (var kind in kinds)
		{
			if (nodeKind == kind)
			{
				return true;
			}
		}

		return false;
	}

	public static bool IsKind(this SyntaxTrivia node, params ReadOnlySpan<SyntaxKind> kinds)
	{
		var nodeKind = node.Kind();

		foreach (var kind in kinds)
		{
			if (nodeKind == kind)
			{
				return true;
			}
		}

		return false;
	}

	public static bool IsKind(this SyntaxToken node, params ReadOnlySpan<SyntaxKind> kinds)
	{
		var nodeKind = node.Kind();

		foreach (var kind in kinds)
		{
			if (nodeKind == kind)
			{
				return true;
			}
		}

		return false;
	}
}

