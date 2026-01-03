using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for idempotent modulo: (x % m) % m => x % m (when m is non-zero constant)
/// </summary>
public class ModuloIdempotencyStrategy : IntegerBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsKind(SyntaxKind.ModuloExpression)
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || rightValue.IsNumericZero()
		    || !LeftEqualsRight(context.Left.Syntax.Right, context.Right.Syntax, context.TryGetLiteral))
			return false;
		
		optimized = context.Left.Syntax;
		return true;
	}
}
