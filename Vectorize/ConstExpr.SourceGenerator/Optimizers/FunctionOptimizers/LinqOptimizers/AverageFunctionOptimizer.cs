using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Average method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Average() =&gt; collection.Average() (skip type cast)
/// - collection.ToList().Average() =&gt; collection.Average() (skip materialization)
/// - collection.ToArray().Average() =&gt; collection.Average() (skip materialization)
/// </summary>
public class AverageFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Average), 0, 1)
{
	// Operations that don't affect Average behavior (type casts and materializations)
	private static readonly HashSet<string> OperationsThatDontAffectAverage =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var originalSource = source;

		// Skip operations that don't affect Average (AsEnumerable, ToList, ToArray)
		while (IsLinqMethodChain(source, OperationsThatDontAffectAverage, out var chainInvocation)
		       && TryGetLinqSource(chainInvocation, out var innerSource))
		{
			source = innerSource;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Average call
		if (source != originalSource)
		{
			// Preserve selector if it exists
			if (parameters.Count > 0)
			{
				result = CreateLinqMethodCall(source, nameof(Enumerable.Average), parameters[0]);
			}
			else
			{
				result = CreateLinqMethodCall(source, nameof(Enumerable.Average));
			}

			return true;
		}

		result = null;
		return false;
	}
}
