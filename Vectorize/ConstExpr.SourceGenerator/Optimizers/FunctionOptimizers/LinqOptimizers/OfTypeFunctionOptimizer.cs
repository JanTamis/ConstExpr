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

		// Get the type argument for this OfType call
		var typeArg = context.Method.TypeArguments[0];

		// Optimize source.OfType<T>().OfType<T>() => source.OfType<T>() (duplicate removal with same type)
		if (IsLinqMethodChain(source, nameof(Enumerable.OfType), out var ofTypeInvocation)
		    && TryGetLinqSource(ofTypeInvocation, out _)
		    && context.Model.GetSymbolInfo(ofTypeInvocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } innerMethod
		    && SymbolEqualityComparer.Default.Equals(innerMethod.TypeArguments[0], typeArg))
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		// Optimize source.Cast<T>().OfType<T>() => source.Cast<T>() (redundant OfType after Cast with same type)
		if (IsLinqMethodChain(source, nameof(Enumerable.Cast), out var castInvocation)
		    && TryGetLinqSource(castInvocation, out _)
		    && context.Model.GetSymbolInfo(castInvocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } innerMethod1
		    && SymbolEqualityComparer.Default.Equals(innerMethod1.TypeArguments[0], typeArg))
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		result = null;
		return false;
	}
}

