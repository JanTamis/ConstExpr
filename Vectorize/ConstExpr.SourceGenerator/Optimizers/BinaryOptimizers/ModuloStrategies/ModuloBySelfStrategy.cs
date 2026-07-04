using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
///   Strategy for modulo by itself: x % x is 0 for any non-zero x, and 0 % 0 throws
///   <see cref="DivideByZeroException" /> at runtime — unlike division, this holds unconditionally
///   (no sign or non-zero proof needed), so it always folds to a guarded conditional.
/// </summary>
public class ModuloBySelfStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized)
		    || !IsPure(context.Left.Syntax)
		    || !LeftEqualsRight(context)
		    || !TryCreateLiteral(0.ToSpecialType(context.Type.SpecialType), out var zero))
		{
			return false;
		}

		optimized = ConditionalExpression(
			BinaryExpression(SyntaxKind.EqualsExpression, context.Left.Syntax, zero!),
			ThrowExpression(ObjectCreationExpression(ParseTypeName(nameof(DivideByZeroException))).WithArgumentList(ArgumentList())),
			zero!);

		return true;
	}
}