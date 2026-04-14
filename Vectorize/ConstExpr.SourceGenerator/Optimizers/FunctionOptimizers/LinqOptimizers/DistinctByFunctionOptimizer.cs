using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.DistinctBy context.Method.
/// Optimizes patterns such as:
/// - collection.DistinctBy(x => x) => collection.Distinct() (identity key selector)
/// - Enumerable.Empty&lt;T&gt;().DistinctBy(selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class DistinctByFunctionOptimizer() : BaseLinqFunctionOptimizer("DistinctBy", 1)
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

		// Optimize DistinctBy(x => x) => Distinct()
		if (IsIdentityLambda(lambda))
		{
			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct)));
			return true;
		}

		result = null;
		return false;
	}
}