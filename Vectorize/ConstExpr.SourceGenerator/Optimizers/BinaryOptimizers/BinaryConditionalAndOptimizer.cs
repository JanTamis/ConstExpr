using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
		if (hasLeftValue && leftValue is false)
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// true && x = x
		if (hasLeftValue && leftValue is true)
		{
			result = Right;
			return true;
		}

		// x && true = x (only if x is pure, to avoid side effects)
		if (hasRightValue && rightValue is true && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// x && false = false (only if x is pure, to avoid side effects)
		if (hasRightValue && rightValue is false && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// x && x = x (for pure expressions)
		if (Left.IsEquivalentTo(Right) && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// Both sides are constant, evaluate
		if (hasLeftValue && hasRightValue)
		{
			var evalResult = ObjectExtensions.ExecuteBinaryOperation(Kind, leftValue, rightValue);
			if (evalResult != null)
			{
				result = SyntaxHelpers.CreateLiteral(evalResult);
				return true;
			}
		}

		return false;
	}
}

