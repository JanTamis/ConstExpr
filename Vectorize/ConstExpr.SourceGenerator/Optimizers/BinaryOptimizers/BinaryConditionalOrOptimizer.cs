using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
		if (hasLeftValue && leftValue is true)
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// false || x = x
		if (hasLeftValue && leftValue is false)
		{
			result = Right;
			return true;
		}

		// x || false = x (only if x is pure, to avoid side effects)
		if (hasRightValue && rightValue is false && IsPure(Left))
		{
			result = Left;
			return true;
		}

		// x || true = true (only if x is pure, to avoid side effects)
		if (hasRightValue && rightValue is true && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// x || x = x (for pure expressions)
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