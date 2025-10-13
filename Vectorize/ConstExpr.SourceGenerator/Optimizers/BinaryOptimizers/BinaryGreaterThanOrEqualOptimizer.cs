using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOrEqualOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThanOrEqual;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x >= 0 = true (when x is unsigned)
			if (Type.IsUnsignedInteger() && rightValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(true);
				return true;
			}

			// 0 >= x = false (when x is positive and non-zero and unsigned)
			if (leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero() 
			    && (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, rightValue, 0.ToSpecialType(Type.SpecialType)) is true))
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}
		}

		return false;
	}
}

