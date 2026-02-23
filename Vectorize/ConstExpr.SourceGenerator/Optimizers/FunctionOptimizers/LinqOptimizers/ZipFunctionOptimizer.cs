using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Zip context.Method.
/// Optimizes patterns such as:
/// - collection.Zip(Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// - Enumerable.Empty&lt;T&gt;().Zip(collection) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// </summary>
public class ZipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Zip), 1, 2)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		var secondSource = context.VisitedParameters[0];
		
		source = context.Visit(source) ?? source;

		// If either source is empty, result is empty
		if (IsEmptyEnumerable(source) 
		    || IsEmptyEnumerable(secondSource))
		{
			// Get the return type element from the context.Method
			if (context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
			{
				result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
				return true;
			}
		}

		result = null;
		return false;
	}
}

