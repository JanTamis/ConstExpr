using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OfType context.Method.
/// Optimizes patterns such as:
/// - collection.OfType&lt;T&gt;().OfType&lt;T&gt;() => collection.OfType&lt;T&gt;() (duplicate removal)
/// - collection.Cast&lt;T&gt;().OfType&lt;T&gt;() => collection.Cast&lt;T&gt;() (redundant OfType after Cast)
/// </summary>
public class OfTypeFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OfType), n => n is 0)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Save the original source (from the unmodified syntax tree) before TryExecutePredicates
		// replaces it with a visited copy.  GetSymbolInfo only works on nodes that belong to the
		// original compilation's syntax tree, so we must use this reference for symbol lookups below.
		var originalSource = source;

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Get the type argument for this OfType call
		var typeArg = context.Method.TypeArguments[0];

		// Use originalSource (original tree node) so that GetSymbolInfo resolves correctly.
		if (IsLinqMethodChain(originalSource, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.OfType) when context.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } ofTypeMethod
				                                    && SymbolEqualityComparer.Default.Equals(ofTypeMethod.TypeArguments[0], typeArg):
				{
					result = invocationSource;
					return true;
				}
				case nameof(Enumerable.Cast) when context.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } castMethod
				                                  && SymbolEqualityComparer.Default.Equals(castMethod.TypeArguments[0], typeArg):
				{
					result = invocationSource;
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}