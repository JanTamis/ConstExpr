using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeLast context.Method.
/// Optimizes patterns such as:
/// - collection.TakeLast(0) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeLastFunctionOptimizer() : BaseLinqFunctionOptimizer("TakeLast", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);
		var amounts = new List<ExpressionSyntax> { context.VisitedParameters[0] };

		while (IsLinqMethodChain(source, "TakeLast", out var takeInvocation)
		       && TryGetLinqSource(takeInvocation, out var takeSource))
		{
			var argument = takeInvocation.ArgumentList.Arguments[0].Expression;

			amounts.Add(argument);

			TryGetOptimizedChainExpression(takeSource, MaterializingMethods, out source);
			isNewSource = true;
		}

		var minAmount = amounts
			.OfType<LiteralExpressionSyntax>()
			.OrderBy(o => o.Token.Value)
			.FirstOrDefault();

		var noValues = amounts
			.Where(w => w is not LiteralExpressionSyntax);

		var amount = noValues
			.Aggregate<ExpressionSyntax, ExpressionSyntax>(minAmount, (acc, next) => CreateInvocation(ParseTypeName("Int32"), "Min", acc, next));

		if (amount is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		if (isNewSource)
		{
			if (TryExecutePredicates(context, source, [ amount ], out result))
			{
				return true;
			}

			result = UpdateInvocation(context, source, amount);
			return true;
		}

		result = null;
		return false;
	}
}

