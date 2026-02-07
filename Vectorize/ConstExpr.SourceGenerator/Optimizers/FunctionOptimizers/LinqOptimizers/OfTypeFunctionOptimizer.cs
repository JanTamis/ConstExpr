using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OfType method.
/// Optimizes patterns such as:
/// - collection.OfType&lt;T&gt;().OfType&lt;T&gt;() => collection.OfType&lt;T&gt;() (duplicate removal)
/// - collection.Cast&lt;T&gt;().OfType&lt;T&gt;() => collection.Cast&lt;T&gt;() (redundant OfType after Cast)
/// </summary>
public class OfTypeFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OfType), 0)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Get the type argument for this OfType call
		var typeArg = method.TypeArguments[0];

		// Optimize source.OfType<T>().OfType<T>() => source.OfType<T>() (duplicate removal with same type)
		if (IsLinqMethodChain(source, nameof(Enumerable.OfType), out var ofTypeInvocation)
		    && TryGetLinqSource(ofTypeInvocation, out _))
		{
			if (model.GetSymbolInfo(ofTypeInvocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } innerMethod 
			    && SymbolEqualityComparer.Default.Equals(innerMethod.TypeArguments[0], typeArg))
			{
				result = source;
				return true;
			}
		}

		// Optimize source.Cast<T>().OfType<T>() => source.Cast<T>() (redundant OfType after Cast with same type)
		if (IsLinqMethodChain(source, nameof(Enumerable.Cast), out var castInvocation)
		    && TryGetLinqSource(castInvocation, out _))
		{
			if (model.GetSymbolInfo(castInvocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } innerMethod 
			    && SymbolEqualityComparer.Default.Equals(innerMethod.TypeArguments[0], typeArg))
			{
				result = source;
				return true;
			}
		}

		result = null;
		return false;
	}
}

