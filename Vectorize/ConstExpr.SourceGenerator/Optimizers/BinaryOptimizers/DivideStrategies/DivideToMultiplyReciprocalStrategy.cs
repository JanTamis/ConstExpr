using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for floating point division to multiplication: x / a => x * (1/a)
/// Requires ReciprocalMath flag as reciprocal approximation may differ from IEEE 754.
/// </summary>
public class DivideToMultiplyReciprocalStrategy : FloatNumberBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.ReciprocalMath;

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || context.Right.Syntax.IsNumericZero())
		{
			return false;
		}
		
		var reciprocal = 1.ToSpecialType(context.Type.SpecialType)
			.Divide(context.Right.Syntax.Token.Value.ToSpecialType(context.Type.SpecialType));

		optimized = BinaryExpression(
			SyntaxKind.MultiplyExpression, 
			context.Left.Syntax, 
			CreateLiteral(reciprocal));

		return true;
	}
}
