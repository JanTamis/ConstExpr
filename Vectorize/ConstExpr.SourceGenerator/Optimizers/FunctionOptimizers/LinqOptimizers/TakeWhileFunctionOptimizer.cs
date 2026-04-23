using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeWhile context.Method.
/// Optimizes patterns such as:
/// - collection.TakeWhile(x => true) => collection (take everything)
/// - collection.TakeWhile(x => false) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.TakeWhile), n => n is 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!TryGetLambda(context.VisitedParameters[0], out var lambda))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsLiteralBooleanLambda(lambda, out var value))
		{
			switch (value)
			{
				// Optimize TakeWhile(x => true) => collection (take everything)
				case true:
					result = source;
					return true;
				// Optimize TakeWhile(x => false) => Enumerable.Empty<T>() (take nothing)
				case false:
					result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
					return true;
			}
		}

		result = null;
		return false;
	}
}