using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderBy), 1)
{
	private static readonly HashSet<string> OrderingOperations =
	[
		..MaterializingMethods,
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending"
	];
	
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

		var isNewSource = TryGetOptimizedChainExpression(source, OrderingOperations, out source);

		// Optimize OrderBy(x => x) => Order() (identity lambda)
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(source, "Order");
			return true;
		}
		
		if (isNewSource)
		{
			result = CreateInvocation(source, Name, lambda);
			return true;
		}
		
		result = null;
		return false;
	}
}