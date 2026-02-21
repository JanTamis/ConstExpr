using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OfType context.Method.
/// Optimizes patterns such as:
/// - collection.OfType&lt;T&gt;().OfType&lt;T&gt;() => collection.OfType&lt;T&gt;() (duplicate removal)
/// - collection.Cast&lt;T&gt;().OfType&lt;T&gt;() => collection.Cast&lt;T&gt;() (redundant OfType after Cast)
/// </summary>
public class OfTypeFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OfType), 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Get the type argument for this OfType call
		var typeArg = context.Method.TypeArguments[0];

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.OfType) when context.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } ofTypeMethod
					&& SymbolEqualityComparer.Default.Equals(ofTypeMethod.TypeArguments[0], typeArg):
				{
					result = context.Visit(invocationSource) ?? invocationSource;
					return true;
				}
				case nameof(Enumerable.Cast) when context.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } castMethod
					&& SymbolEqualityComparer.Default.Equals(castMethod.TypeArguments[0], typeArg):
				{
					result = context.Visit(invocationSource) ?? invocationSource;
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}

