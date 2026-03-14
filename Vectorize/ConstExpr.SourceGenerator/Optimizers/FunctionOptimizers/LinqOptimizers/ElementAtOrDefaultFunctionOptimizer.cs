using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
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
/// - Enumerable.Range(start, count).ElementAtOrDefault(n) => n >= 0 &amp;&amp; n &lt; count ? start + n : default
/// - Enumerable.Repeat(element, count).ElementAtOrDefault(n) => n >= 0 &amp;&amp; n &lt; count ? element : default
/// Note: We can't optimize to direct array/list indexing because those throw exceptions for out-of-bounds,
/// while ElementAtOrDefault returns default value.
/// Note: OrderBy/OrderByDescending/Reverse DOES affect element positions, so we don't optimize those!
/// Note: Distinct/Where/Select change the collection, so we don't optimize those either!
/// </summary>
public class ElementAtOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ElementAtOrDefault), 1)
{
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
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var type = context.Method.ReturnType;

		while (IsLinqMethodChain(source, nameof(Enumerable.Skip), out var skipInvocation)
		       && TryGetLinqSource(skipInvocation, out source)
		       && GetMethodArguments(skipInvocation).FirstOrDefault() is { Expression: { } skipCount })
		{
			var tempResult = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, indexParameter, skipCount);

			indexParameter = context.OptimizeBinaryExpression(tempResult, type, type, type) as ExpressionSyntax;
			isNewSource = true;

			TryGetOptimizedChainExpression(source, MaterializingMethods, out source);
		}

		if (TryExecutePredicates(context, source, [indexParameter], out result))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out _))
		{
			var intType = context.Model.Compilation.CreateInt32();
			var boolType = context.Model.Compilation.CreateBoolean();

			switch (methodName)
			{
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					// Range(start, count).ElementAtOrDefault(n) => n >= 0 && n < count ? start + n : default(int)
					var indexNonNeg = OptimizeComparison(context, SyntaxKind.GreaterThanOrEqualExpression, indexParameter, SyntaxHelpers.CreateLiteral(0)!, intType);
					var indexInBounds = OptimizeComparison(context, SyntaxKind.LessThanExpression, indexParameter, countArg.Expression, intType);
					var condition = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, indexNonNeg, indexInBounds, boolType);

					result = SyntaxFactory.ConditionalExpression(
						condition,
						OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, indexParameter, intType),
						type.GetDefaultValue());
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					// Repeat(element, count).ElementAtOrDefault(n) => n >= 0 && n < count ? element : default(T)
					var indexNonNeg = OptimizeComparison(context, SyntaxKind.GreaterThanOrEqualExpression, indexParameter, SyntaxHelpers.CreateLiteral(0)!, intType);
					var indexInBounds = OptimizeComparison(context, SyntaxKind.LessThanExpression, indexParameter, repeatCountArg.Expression, intType);
					var condition = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, indexNonNeg, indexInBounds, boolType);

					result = SyntaxFactory.ConditionalExpression(condition, repeatElementArg.Expression, type.GetDefaultValue());
					return true;
				}
			}
		}

		if (IsInvokedOnArray(context, source))
		{
			// optimize to x.Length > n ? x[n] : default
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
			// optimize to x.Count > n ? x[n] : default
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