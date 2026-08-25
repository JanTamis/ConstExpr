using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
///   Strategy for bitwise self-complement: x &amp; ~x = 0 (pure). Every bit that's set in x is
///   clear in ~x and vice versa, so the AND always clears every bit.
/// </summary>
public class AndSelfComplementStrategy() : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.BitwiseNotExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Operand, context.Variables)
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
