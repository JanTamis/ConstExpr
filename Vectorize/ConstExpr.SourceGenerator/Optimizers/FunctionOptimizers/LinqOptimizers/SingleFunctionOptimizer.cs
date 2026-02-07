using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Single method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Single() => collection.Single(predicate)
/// - collection.AsEnumerable().Single() => collection.Single()
/// - collection.ToList().Single() => collection.Single()
/// </summary>
public class SingleFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Single), 0, 1)
{
	// Operations that don't affect which element is "single"
	private static readonly HashSet<string> OperationsThatDontAffectSingle =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect single
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSingle, out source);

		// Optimize source.Where(predicate).Single() => source.Single(predicate)
		if (parameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && TryGetLinqSource(whereInvocation, out var whereSource)
		    && whereInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectSingle, out whereSource);
			
			var predicate = whereInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(whereSource, nameof(Enumerable.Single), predicate);
			return true;
		}

		// If we skipped any operations, create optimized Single() call
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.Single), parameters);
			return true;
		}

		result = null;
		return false;
	}
}
