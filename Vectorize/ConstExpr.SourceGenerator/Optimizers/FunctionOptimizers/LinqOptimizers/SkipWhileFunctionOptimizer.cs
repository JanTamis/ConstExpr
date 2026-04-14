using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipWhile context.Method.
/// Optimizes patterns such as:
/// - collection.SkipWhile(x => false) => collection (skip nothing)
/// - collection.SkipWhile(x => true) => Enumerable.Empty&lt;T&gt;() (skip everything)
/// </summary>
public class SkipWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SkipWhile), 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!TryGetLambda(context.VisitedParameters[0], out var lambda))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		if (IsLiteralBooleanLambda(lambda, out var value))
		{
			switch (value)
			{
				case false:
				{
					result = source;
					return true;
				}
				case true:
				{
					result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}

