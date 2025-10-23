using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Identity element: x & 0 = 0 (for numeric types), x & true = x, x & false = false (for boolean type)
/// </summary>
public class AndIdentityElementStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		// Right is a literal
		return context.Right.Value is bool || context.Right.Value.IsNumericZero();
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		switch (context.Right.Value)
		{
			case false:
				return SyntaxHelpers.CreateLiteral(false);
			case true:
				return context.Left.Syntax;
		}

		// Numeric types: x & 0 = 0, 0 & x = 0
		if (context.Right.Value.IsNumericZero())
		{
			return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		}

		return null;
	}
}