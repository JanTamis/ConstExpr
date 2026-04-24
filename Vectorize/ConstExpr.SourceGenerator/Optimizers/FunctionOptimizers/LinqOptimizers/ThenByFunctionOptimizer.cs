using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ThenBy context.Method.
/// Optimizes patterns such as:
/// - OrderBy(x => x).ThenBy(y => y) => Order().ThenBy(y => y) (identity key for Order)
/// </summary>
public class ThenByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ThenBy), n => n is 1)
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

		if (IsIdentityLambda(lambda))
		{
			result = source;
			return true;
		}

		result = null;
		return false;
	}
}