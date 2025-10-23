using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for identity element optimization: x + 0 = x and 0 + x = x
/// </summary>
public class AddIdentityElementStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		// x + 0 = x
		if (context.Right.HasValue && context.Right.Value.IsNumericZero())
		{
			return true;
		}

		// 0 + x = x
		if (context.Left.HasValue && context.Left.Value.IsNumericZero())
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// x + 0 = x
		if (context.Right.HasValue && context.Right.Value.IsNumericZero())
		{
			return context.Left.Syntax;
		}

		// 0 + x = x
		if (context.Left.HasValue && context.Left.Value.IsNumericZero())
		{
			return context.Right.Syntax;
		}

		return null;
	}
}
