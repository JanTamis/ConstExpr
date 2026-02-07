using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Aggregate method.
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

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAggregate, out source);

		// First, try to optimize Aggregate to Sum if it's just adding values
		if (TryOptimizeAggregateToSum(method, parameters, source!, visit, out result))
		{
			if (IsLinqMethodChain(source, nameof(Enumerable.Select), out var innerInvocation)
			    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda)
			    && TryGetLinqSource(innerInvocation, out var innerSource))
			{
				if (method.Parameters.Length == 2 && !IsZeroLiteral(parameters[0]))
				{
					result = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, CreateInvocation(visit(innerSource) ?? innerSource, nameof(Enumerable.Count), visit(innerLambda) ?? innerSource), parameters[0]);
				}
				else
				{
					result = CreateInvocation(visit(innerSource) ?? innerSource, nameof(Enumerable.Aggregate), visit(innerLambda) ?? innerSource);
				}
			}
			
			return true;
		}

		if (IsZeroLiteral(parameters[0]))
		{
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Aggregate), parameters.Skip(1));
			return true;
		}

		if (isNewSource)
		{
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Aggregate), parameters);
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
	private bool TryOptimizeAggregateToSum(IMethodSymbol method, 
		IList<ExpressionSyntax> parameters, ExpressionSyntax source, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		result = null;

		// Check parameter count: 1 or 2 (we don't optimize the 3-parameter overload with result selector)
		if (method.Parameters.Length is not 1 and not 2)
    {
      return false;
    }

    // Get the lambda (last parameter)
    var lambdaArg = parameters[^1];
		
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
    result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Sum));

		if (method.Parameters.Length == 2 && !IsZeroLiteral(parameters[0]))
		{
			result = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, (ExpressionSyntax)result, parameters[0]);
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
		// Get parameters
		string? accParam = null;
		string? valueParam = null;

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
}
