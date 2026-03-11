using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MathMaxOptimizer = ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MaxFunctionOptimizer;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.MaxBy context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().MaxBy(selector) - cannot optimize (throws exception)
/// </summary>
public class MaxByFunctionOptimizer() : BaseLinqFunctionOptimizer("MaxBy", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(source, nameof(Enumerable.Max));
			return true;
		}

		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, MaterializingMethods, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var leftExpr = left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters);
					var rightExpr = right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters);

					result = OptimizeAsMathPairwise<MathMaxOptimizer>(context, leftExpr, rightExpr);
					return true;
				}
			}
		}
		
		result = null;
		return false;
	}
}

