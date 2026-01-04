using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SymmetricStrategy<TStrategy, TLeft, TRight> : BaseBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
	where TStrategy : IBinaryStrategy<ExpressionSyntax, ExpressionSyntax>, new()
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	private readonly TStrategy _innerStrategy = new();

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!_innerStrategy.TryOptimize(context, out optimized))
			return false;

		if (context.Left.Syntax is TLeft left 
		    && context.Right.Syntax is TRight right)
		{
			var newContext = new BinaryOptimizeContext<TLeft, TRight>
			{
				Left = new BinaryOptimizeElement<TLeft>()
				{
					Syntax = left,
					Type = context.Left.Type,
				},
				Right = new BinaryOptimizeElement<TRight>()
				{
					Syntax = right,
					Type = context.Right.Type,
				},
				Type = context.Type,
			};

			if (TryOptimizeSymmetric(newContext, out optimized))
			{
				return true;
			}
		}

		if (context.Right.Syntax is TRight swappedRight
		    && context.Left.Syntax is TLeft swappedLeft)
		{
			var swappedContext = new BinaryOptimizeContext<TLeft, TRight>
			{
				Left = new BinaryOptimizeElement<TLeft>()
				{
					Syntax = swappedLeft,
					Type = context.Right.Type,
				},
				Right = new BinaryOptimizeElement<TRight>()
				{
					Syntax = swappedRight,
					Type = context.Left.Type,
				},
				Type = context.Type,
			};

			if (TryOptimizeSymmetric(swappedContext, out optimized))
			{
				return true;
			}
		}
		
		return false;
	}
	
	public abstract bool TryOptimizeSymmetric(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized);
}