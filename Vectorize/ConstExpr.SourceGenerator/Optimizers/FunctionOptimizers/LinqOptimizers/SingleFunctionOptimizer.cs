using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Single context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Single() => collection.Single(predicate)
/// - collection.AsEnumerable().Single() => collection.Single()
/// - collection.ToList().Single() => collection.Single()
/// </summary>
public class SingleFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Single), n => n is 0 or 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip operations that don't affect single
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize source.Where(predicate).Single() => source.Single(predicate)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && TryGetLinqSource(whereInvocation, out var whereSource)
		    && whereInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(whereSource, MaterializingMethods, out whereSource);

			var predicate = whereInvocation.ArgumentList.Arguments[0].Expression;

			result = UpdateInvocation(context, whereSource, predicate);
			return true;
		}

		// If we skipped any operations, create optimized Single() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}