using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public abstract BinaryOperatorKind Kind { get; }

	public abstract IEnumerable<IBinaryStrategy<TLeft, TRight>> GetStrategies();
}