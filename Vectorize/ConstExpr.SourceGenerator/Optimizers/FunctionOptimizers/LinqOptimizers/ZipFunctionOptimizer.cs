using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Zip method.
/// Optimizes patterns such as:
/// - collection.Zip(Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// - Enumerable.Empty&lt;T&gt;().Zip(collection) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// </summary>
public class ZipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Zip), 1, 2)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = parameters[0];

		// If either source is empty, result is empty
		if (IsEmptyEnumerable(source) || IsEmptyEnumerable(secondSource))
		{
			// Get the return type element from the method
			if (method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
			{
				result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
				return true;
			}
		}

		result = null;
		return false;
	}
}

