using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryRightShiftOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.RightShift;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		// Only integer-like types participate in shifts
		if (!Type.IsInteger())
		{
			return false;
		}

		// x >> 0 => x (require Right to be pure to avoid dropping side effects)
		if (Right.TryGetLiteralValue(loader, variables, out var rightValue) 
		    && rightValue.IsNumericZero())
		{
			result = Left;
			return true;
		}

		// 0 >> x => 0 (require Right to be pure to avoid dropping side effects)
		if (Left.TryGetLiteralValue(loader, variables, out var leftValue) 
		    && leftValue.IsNumericZero() 
		    && IsPure(Right))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// ((x >> a) >> b) => x >> (a + b) - combine shifts
		if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.RightShiftExpression } leftShift
		    && Right.TryGetLiteralValue(loader, variables, out var rightShiftValue) && rightShiftValue != null
		    && leftShift.Right.TryGetLiteralValue(loader, variables, out var leftShiftValue) && leftShiftValue != null)
		{
			var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Add, leftShiftValue, rightShiftValue);
			if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
			{
				result = BinaryExpression(SyntaxKind.RightShiftExpression, leftShift.Left, combinedLiteral);
				return true;
			}
		}

		return false;
	}
}
