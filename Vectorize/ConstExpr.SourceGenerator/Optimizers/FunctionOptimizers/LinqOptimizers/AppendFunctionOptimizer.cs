using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Append method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Append(x) =&gt; collection.Append(x) (skip type cast)
/// - collection.ToList().Append(x) =&gt; collection.Append(x) (skip materialization)
/// - collection.ToArray().Append(x) =&gt; collection.Append(x) (skip materialization)
/// </summary>
public class AppendFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Append), 1)
{
	// Operations that don't affect Append behavior (type casts and materializations)
	private static readonly HashSet<string> OperationsThatDontAffectAppend =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLinqSource(invocation, out var source)
		    || parameters.Count == 0)
		{
			result = null;
			return false;
		}

		var appendedValue = parameters[0];
		var originalSource = source;

		// Skip operations that don't affect Append (AsEnumerable, ToList, ToArray)
		while (IsLinqMethodChain(source, OperationsThatDontAffectAppend, out var chainInvocation)
		       && TryGetLinqSource(chainInvocation, out var innerSource))
		{
			source = innerSource;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Append call
		if (source != originalSource)
		{
			result = CreateLinqMethodCall(source, nameof(Enumerable.Append), appendedValue);
			return true;
		}

		result = null;
		return false;
	}
}
