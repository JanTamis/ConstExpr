using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalAnd;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// false && x = false
		if (leftValue is false)
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// true && x = x
		if (leftValue is true)
		{
			result = Right;
			return true;
		}

		// x && true = x (only if x is pure, to avoid side effects)
		if (rightValue is true && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// x && false = false (only if x is pure, to avoid side effects)
		if (rightValue is false && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// x && x = x (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// Absorption law: a && (a || b) => a (pure)
		if (Right is Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax { RawKind: (int) Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression } rightOr
		    && IsPure(Left))
		{
			if (rightOr.Left.IsEquivalentTo(Left) || rightOr.Right.IsEquivalentTo(Left))
			{
				result = Left;
				return true;
			}
		}

		// Absorption law: (a || b) && a => a (pure)
		if (Left is Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax { RawKind: (int) Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression } leftOr
		    && IsPure(Right))
		{
			if (leftOr.Left.IsEquivalentTo(Right) || leftOr.Right.IsEquivalentTo(Right))
			{
				result = Right;
				return true;
			}
		}

		// Redundancy: (a && b) && a => a && b (already covered by left side, pure)
		if (Right is Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax { RawKind: (int) Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression } rightAnd
		    && IsPure(Left))
		{
			if (rightAnd.Left.IsEquivalentTo(Left) || rightAnd.Right.IsEquivalentTo(Left))
			{
				result = Right;
				return true;
			}
		}

		// a && !a => false (contradiction, pure)
		if (Right is Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax { RawKind: (int) Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalNotExpression } rightNot
		    && rightNot.Operand.IsEquivalentTo(Left)
		    && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// !a && a => false (contradiction, pure)
		if (Left is Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax { RawKind: (int) Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalNotExpression } leftNot
		    && leftNot.Operand.IsEquivalentTo(Right)
		    && IsPure(Right))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		return false;
	}
}

