using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
		..MaterializingMethods,
		..OrderingOperations,
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAggregate, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// First, try to optimize Aggregate to Sum if it's just adding values
		if (TryOptimizeAggregateToSum(context, source, out result))
		{
			return true;
		}

		if (IsZeroLiteral(context.VisitedParameters[0]))
		{
			result = UpdateInvocation(context, source, context.VisitedParameters.Skip(1));
			return true;
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
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
	/// - Aggregate(0, (acc, v) => acc + v, acc => acc * 2) => Sum() << 1
	/// </summary>
	private bool TryOptimizeAggregateToSum(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result)
	{
		result = null;

		if (context.Method.Parameters.Length == 0)
		{
			return false;
		}

		// Get the accumulator lambda (second parameter for 2/3-arg overloads, first for 1-arg)
		var lambdaArg = context.VisitedParameters.Count switch
		{
			1 => context.VisitedParameters[0], // Aggregate(lambda)
			2 or 3 => context.VisitedParameters[1], // Aggregate(seed, lambda) or Aggregate(seed, lambda, resultSelector)
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
		result = TryOptimizeByOptimizer<SumFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Sum)), x => IsEnumerableType(x.Parameters[0].Type as INamedTypeSymbol, context.Method.TypeArguments[^1]), []);

		// For 2-arg overload with non-zero seed: Sum() + seed
		if (context.VisitedParameters.Count == 2 && !IsZeroLiteral(context.VisitedParameters[0]))
		{
			var type = context.Method.TypeArguments[^1];
			result = OptimizeArithmetic(context, SyntaxKind.AddExpression, (ExpressionSyntax) result, context.VisitedParameters[0], type);
		}

		// For 3-arg overload: apply the result selector to the sum result
		if (context.VisitedParameters.Count == 3)
		{
			// For non-zero seed, add the seed first
			if (!IsZeroLiteral(context.VisitedParameters[0]))
			{
				var type = context.Method.TypeArguments[^1];
				result = OptimizeArithmetic(context, SyntaxKind.AddExpression, (ExpressionSyntax) result, context.VisitedParameters[0], type);
			}

			// Apply the result selector lambda: e.g. acc => acc * 2 applied to Sum() gives Sum() * 2 => Sum() << 1
			if (TryGetLambda(context.VisitedParameters[2], out var resultSelectorLambda))
			{
				result = ReplaceExpression(resultSelectorLambda, (ExpressionSyntax) result);
				result = context.Visit((ExpressionSyntax) result) ?? result;
			}
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
		if (body is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression } binaryExpr)
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