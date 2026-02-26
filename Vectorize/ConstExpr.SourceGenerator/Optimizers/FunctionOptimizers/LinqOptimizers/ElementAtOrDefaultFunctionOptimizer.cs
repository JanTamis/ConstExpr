using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ElementAtOrDefault context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (type cast doesn't affect indexing)
/// - collection.ToList().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (materialization doesn't affect indexing)
/// - collection.ToArray().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (materialization doesn't affect indexing)
/// - collection.ElementAtOrDefault(0) => collection.FirstOrDefault() (semantically equivalent, more idiomatic)
/// - collection.Skip(n).ElementAtOrDefault(m) => collection.ElementAtOrDefault(n + m) (index adjustment for Skip)
/// Note: We can't optimize to direct array/list indexing because those throw exceptions for out-of-bounds,
/// while ElementAtOrDefault returns default value.
/// Note: OrderBy/OrderByDescending/Reverse DOES affect element positions, so we don't optimize those!
/// Note: Distinct/Where/Select change the collection, so we don't optimize those either!
/// </summary>
public class ElementAtOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ElementAtOrDefault), 1)
{
	// Operations that don't affect element positions or indexing
	// We CAN'T include ordering operations because they change element positions!
	// We CAN'T include filtering/projection operations because they change the collection!
	private static readonly HashSet<string> OperationsThatDontAffectIndexing =
	[
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray), // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source)
		    || context.VisitedParameters.Count == 0)
		{
			result = null;
			return false;
		}

		var indexParameter = context.VisitedParameters[0];

		// Recursively skip all operations that don't affect indexing
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectIndexing, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		source = context.Visit(source) ?? source;
		
		var type = context.Method.ReturnType;

		while (IsLinqMethodChain(source, nameof(Enumerable.Skip), out var skipInvocation)
		       && TryGetLinqSource(skipInvocation, out source)
		       && GetMethodArguments(skipInvocation).FirstOrDefault() is { Expression: { } skipCount })
		{
			var tempResult = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, indexParameter, skipCount);
			
			indexParameter = context.OptimizeBinaryExpression(tempResult, type, type, type) as ExpressionSyntax;
			isNewSource = true;

			TryGetOptimizedChainExpression(source, OperationsThatDontAffectIndexing, out source);
		}

		if (IsInvokedOnArray(context, source))
		{
			source = context.Visit(source) ?? source;
			
			// optimize to x.Length > 1 ? x[1] : 0;
			result = SyntaxFactory.ConditionalExpression(
				SyntaxFactory.BinaryExpression(
					SyntaxKind.GreaterThanExpression, CreateMemberAccess(source, "Length"),
					indexParameter!),
				CreateElementAccess(source, indexParameter),
				type.GetDefaultValue());
			return true;
		}

		if (IsInvokedOnList(context, source))
		{
			source = context.Visit(source) ?? source;
			
			// optimize to x.Count > 1 ? x[1] : 0;
			result = SyntaxFactory.ConditionalExpression(
				SyntaxFactory.BinaryExpression(
					SyntaxKind.GreaterThanExpression, CreateMemberAccess(source, "Count"),
					indexParameter!),
				CreateElementAccess(source, indexParameter),
				type.GetDefaultValue());
		}

		if (indexParameter is LiteralExpressionSyntax { Token.Value: 0 })
		{
			result = TryOptimizeByOptimizer<FirstOrDefaultFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.FirstOrDefault)));
			return true;
		}

		// If we skipped any operations, create optimized ElementAtOrDefault() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source, indexParameter);
			return true;
		}

		result = null;
		return false;
	}
}