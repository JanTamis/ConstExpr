using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for floating point division to multiplication: x / a => x * (1/a)
/// </summary>
public class DivideToMultiplyReciprocalStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (context.Type.IsInteger())
		{
			return false;
		}

		if (context.Right is not { HasValue: true, Value: { } rightValue })
		{
			return false;
		}

		if (rightValue.IsNumericZero())
		{
			return false;
		}

		var reciprocal = ObjectExtensions.ExecuteBinaryOperation(
			BinaryOperatorKind.Divide, 
			1.ToSpecialType(context.Type.SpecialType), 
			rightValue.ToSpecialType(context.Type.SpecialType));

		return reciprocal is not null;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Right.Value is not { } rightValue)
		{
			return null;
		}

		var reciprocal = ObjectExtensions.ExecuteBinaryOperation(
			BinaryOperatorKind.Divide, 
			1.ToSpecialType(context.Type.SpecialType), 
			rightValue.ToSpecialType(context.Type.SpecialType));

		return SyntaxFactory.BinaryExpression(
			SyntaxKind.MultiplyExpression, 
			context.Left.Syntax, 
			SyntaxHelpers.CreateLiteral(reciprocal)!);
	}
}
