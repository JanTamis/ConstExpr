using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalOr;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// true || x = true
		if (leftValue is true)
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// false || x = x
		if (leftValue is false)
		{
			result = Right;
			return true;
		}

		// x || false = x (only if x is pure, to avoid side effects)
		if (rightValue is false && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// x || true = true (only if x is pure, to avoid side effects)
		if (rightValue is true && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// x || x = x (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// Absorption law: a || (a && b) => a (pure)
		if (Right is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } rightAnd
		    && IsPure(Left))
		{
			if (rightAnd.Left.IsEquivalentTo(Left) || rightAnd.Right.IsEquivalentTo(Left))
			{
				result = Left;
				return true;
			}
		}

		// Absorption law: (a && b) || a => a (pure)
		if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } leftAnd
		    && IsPure(Right))
		{
			if (leftAnd.Left.IsEquivalentTo(Right) || leftAnd.Right.IsEquivalentTo(Right))
			{
				result = Right;
				return true;
			}
		}

		// Redundancy: (a || b) || a => a || b (already covered by left side, pure)
		if (Right is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } rightOr
		    && IsPure(Left))
		{
			if (rightOr.Left.IsEquivalentTo(Left) || rightOr.Right.IsEquivalentTo(Left))
			{
				result = Right;
				return true;
			}
		}

		// a || !a => true (tautology, pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } rightNot
		    && rightNot.Operand.IsEquivalentTo(Left)
		    && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// !a || a => true (tautology, pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } leftNot
		    && leftNot.Operand.IsEquivalentTo(Right)
		    && IsPure(Right))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		return false;
	}
}