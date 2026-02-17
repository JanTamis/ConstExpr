using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Aggregate context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Aggregate(...) => collection.Aggregate(...)
/// - collection.ToList().Aggregate(...) => collection.Aggregate(...)
/// - collection.ToArray().Aggregate(...) => collection.Aggregate(...)
/// - collection.Aggregate((acc, v) => acc + v) => collection.Sum()
/// - collection.Aggregate(0, (acc, v) => acc + v) => collection.Sum()
/// Note: We do NOT optimize Select, Distinct, Where, etc. before Aggregate
/// because they change the elements/order being aggregated over.
/// </summary>
public class AggregateFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Aggregate), 1, 2, 3)
{
	// Only operations that don't change elements, order, or filtering
	// These are essentially no-ops that just change the type or materialize
	private static readonly HashSet<string> OperationsThatDontAffectAggregate =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: creates list but doesn't change elements
		nameof(Enumerable.ToArray),          // Materialization: creates array but doesn't change elements
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAggregate, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// First, try to optimize Aggregate to Sum if it's just adding values
		if (TryOptimizeAggregateToSum(context, source, out result))
		{
			if (context.Method.Parameters.Length == 3
			    && TryGetLambda(context.VisitedParameters[2], out var resultSelectorLambda)
			    && resultSelectorLambda.IsSimpleLambda()
			    && result is ExpressionSyntax syntax)
			{
				result = ReplaceExpression(resultSelectorLambda, syntax);
			}
			
			
			if (IsLinqMethodChain(source, nameof(Enumerable.Select), out var innerInvocation)
			    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda)
			    && TryGetLinqSource(innerInvocation, out var innerSource))
			{
				if (context.Method.Parameters.Length == 2 && !IsZeroLiteral(context.VisitedParameters[0]))
				{
					result = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, CreateInvocation(context.Visit(innerSource) ?? innerSource, nameof(Enumerable.Count), context.Visit(innerLambda) ?? innerSource), context.VisitedParameters[0]);
				}
				else
				{
					result = CreateInvocation(context.Visit(innerSource) ?? innerSource, nameof(Enumerable.Aggregate), context.Visit(innerLambda) ?? innerSource);
				}
			}
			
			return true;
		}

		if (IsZeroLiteral(context.VisitedParameters[0]))
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Aggregate), context.VisitedParameters.Skip(1));
			return true;
		}

		if (isNewSource)
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Aggregate), context.VisitedParameters);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Tries to optimize Aggregate to Sum when the pattern matches simple addition.
	/// Patterns:
	/// - Aggregate((acc, v) => acc + v) => Sum()
	/// - Aggregate(0, (acc, v) => acc + v) => Sum()
	/// </summary>
	private bool TryOptimizeAggregateToSum(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result)
	{
		result = null;

		// Check parameter count: 1 or 2 (we don't optimize the 3-parameter overload with result selector)
		if (context.Method.Parameters.Length == 0)
    {
      return false;
    }

    // Get the lambda (last parameter)
    var lambdaArg = context.VisitedParameters.Count switch
    {
	    1 => context.VisitedParameters[0], // Aggregate(lambda)
	    2 or 3 => context.VisitedParameters[1], // Aggregate(seed, lambda)
	    _ => null
    };
		
		if (!TryGetLambda(lambdaArg, out var lambda))
    {
      return false;
    }

    // Check if lambda is addition: (acc, v) => acc + v
    if (!IsAdditionLambda(lambda))
    {
      return false;
    }

    // Optimize to Sum()
    result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Sum));

		if (context.Method.Parameters.Length == 2 && !IsZeroLiteral(context.VisitedParameters[0]))
		{
			result = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, (ExpressionSyntax)result, context.VisitedParameters[0]);
		}
		
		return true;
	}

	/// <summary>
	/// Checks if the expression is a zero literal (0, 0L, 0f, 0.0, etc.)
	/// </summary>
	private bool IsZeroLiteral(ExpressionSyntax expression)
	{
		return expression is LiteralExpressionSyntax literal && literal.Token.Value switch
		{
			int i => i == 0,
			long l => l == 0L,
			float f => f == 0f,
			double d => d == 0.0,
			decimal m => m == 0m,
			_ => false
		};
	}

	/// <summary>
	/// Checks if a lambda is a simple addition: (acc, v) => acc + v
	/// </summary>
	private bool IsAdditionLambda(LambdaExpressionSyntax lambda)
	{
		// Get context.Parameters
		string? accParam;
		string? valueParam;

		switch (lambda)
		{
			case SimpleLambdaExpressionSyntax:
				// Simple lambda has only 1 parameter, we need 2
				return false;

			case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } parenthesized:
				accParam = parenthesized.ParameterList.Parameters[0].Identifier.Text;
				valueParam = parenthesized.ParameterList.Parameters[1].Identifier.Text;
				break;

			default:
				return false;
		}

		// Get body
		if (!TryGetLambdaBody(lambda, out var body))
    {
      return false;
    }

    // Check if body is: acc + v (binary addition)
    if (body is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } binaryExpr)
    {
      return false;
    }

    // Check if it's acc + v or v + acc
    var isAccLeft = binaryExpr.Left is IdentifierNameSyntax leftId && leftId.Identifier.Text == accParam;
		var isValueRight = binaryExpr.Right is IdentifierNameSyntax rightId && rightId.Identifier.Text == valueParam;

		if (isAccLeft && isValueRight)
    {
      return true;
    }

    // Also check reverse: v + acc
    var isValueLeft = binaryExpr.Left is IdentifierNameSyntax leftId2 && leftId2.Identifier.Text == valueParam;
		var isAccRight = binaryExpr.Right is IdentifierNameSyntax rightId2 && rightId2.Identifier.Text == accParam;

		return isValueLeft && isAccRight;
	}

	private ExpressionSyntax ReplaceExpression(LambdaExpressionSyntax lambda, ExpressionSyntax expression)
	{
		var param = GetLambdaParameter(lambda);
		var body = GetLambdaBody(lambda);

		// Replace the outer lambda's parameter with the inner lambda's body
		return ReplaceIdentifier(body, param, expression);
	}
}
