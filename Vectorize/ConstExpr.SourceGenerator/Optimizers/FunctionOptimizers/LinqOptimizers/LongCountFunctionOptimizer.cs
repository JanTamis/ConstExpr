using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.LongCount context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).LongCount() => collection.LongCount(predicate)
/// - collection.Where(p1).Where(p2).LongCount() => collection.LongCount(p1 && p2) (multiple chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3).LongCount() => collection.LongCount(p1 && p2 && p3)
/// - collection.Select(...).LongCount() => collection.LongCount() (projection doesn't affect count for non-null elements)
/// - collection.OrderBy(...).LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.OrderByDescending(...).LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.Order().LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.OrderDescending().LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.ThenBy(...).LongCount() => collection.LongCount() (secondary ordering doesn't affect count)
/// - collection.ThenByDescending(...).LongCount() => collection.LongCount() (secondary ordering doesn't affect count)
/// - collection.Reverse().LongCount() => collection.LongCount() (reversing doesn't affect count)
/// - collection.AsEnumerable().LongCount() => collection.LongCount() (type cast doesn't affect count)
/// - collection.OrderBy(...).Where(p1).Where(p2).LongCount() => collection.LongCount(p1 && p2) (combining operations)
/// </summary>
public class LongCountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.LongCount), 0, 1)
{
	// Operations that don't affect element count (only order/form but not filtering)
	// Note: We DON'T include Distinct, ToList, ToArray because they might affect count
	// - Distinct: reduces count by removing duplicates
	// - ToList/ToArray: materialization could fail/filter
	private static readonly HashSet<string> OperationsThatDontAffectCount =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Select) // Projection: doesn't change count for non-nullable types
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax>();

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);
		
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out _))
		{
			return true;
		}

		// Walk through the chain and collect all Where statements
		while (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		       && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		       && TryGetLambda(predicateArg, out var predicate)
		       && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			if (IsLiteralBooleanLambda(predicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true:
						TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out source);
						continue;
					case false:
						result = CreateLiteral(0L);
						return true;
				}
			}

			wherePredicates.Add(predicate);

			source = whereSource;

			// Skip operations that don't affect count before the next Where
			isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source) || isNewSource;
		}

		var currentSource = context.Visit(source) ?? source;

		// If we found any Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// try to execute predicates directly if we can get the values at compile time (e.g. for arrays or collections with known contents)
			if (TryGetValues(currentSource, out var values))
			{
				var lambdas = wherePredicates
					.Select(s => context.GetLambda(s)?.Compile())
					.Where(w => w != null)
					.ToList();

				if (lambdas.Count == wherePredicates.Count)
				{
					var count = values.Count(value => lambdas.All(lambda => lambda?.DynamicInvoke(value) is true));

					result = CreateLiteral((long) count);
					return true;
				}
			}

			// Start with the first predicate and combine with the rest
			var combinedPredicate = context.Visit(wherePredicates[^1]) as LambdaExpressionSyntax ?? wherePredicates[^1];

			// Combine from right to left (last to first)
			for (var i = wherePredicates.Count - 2; i >= 0; i--)
			{
				var currentPredicate = context.Visit(wherePredicates[i]) as LambdaExpressionSyntax ?? wherePredicates[i];
				combinedPredicate = CombinePredicates(currentPredicate, combinedPredicate);
			}

			// If Count() has a predicate parameter, combine it as well
			if (context.VisitedParameters is [ LambdaExpressionSyntax lambda ])
			{
				combinedPredicate = CombinePredicates(lambda, combinedPredicate);
			}

			combinedPredicate = context.Visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true when TryOptimizeCollection(context, currentSource, out result):
						return true;
					case false:
						result = CreateLiteral(0L);
						break;
				}
			}

			if (IsLinqMethodChain(currentSource, nameof(Enumerable.DefaultIfEmpty), out _))
			{
				TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.DefaultIfEmpty) ]).ToSet(), out currentSource);

				result = TryOptimizeByOptimizer<LongCountFunctionOptimizer>(context, CreateInvocation(currentSource, nameof(Enumerable.LongCount), combinedPredicate));
				result = CreateInvocation(ParseTypeName("Int64"), "Max", result as ExpressionSyntax, CreateLiteral(1L));
				return true;
			}

			if (IsEmptyEnumerable(currentSource))
			{
				result = CreateLiteral(0L);
				return true;
			}

			result = UpdateInvocation(context, currentSource, combinedPredicate);
			return true;
		}

		if (context.VisitedParameters.Count == 0)
		{
			if (IsLinqMethodChain(currentSource, out var methodName, out var invocation)
			    && TryGetLinqSource(invocation, out var methodSource))
			{
				switch (methodName)
				{
					case "Chunk" when invocation.ArgumentList.Arguments is [ var chunkSizeArg ]:
					{
						var chunkSize = chunkSizeArg.Expression;

						if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
						{
							currentSource = methodSource;
						}
						else
						{
							var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int64);
							var newChunkSource = methodSource;

							if (!TryOptimizeCollection(context, newChunkSource, out var countInvocation))
							{
								countInvocation = CreateSimpleInvocation(newChunkSource, nameof(Enumerable.LongCount));
							}

							var chunkMinus1 = OptimizeArithmetic(context, SyntaxKind.SubtractExpression, chunkSize, CreateLiteral(1L), intType);
							var left = OptimizeArithmetic(context, SyntaxKind.AddExpression, countInvocation as ExpressionSyntax, chunkMinus1, intType);

							result = OptimizeArithmetic(context, SyntaxKind.DivideExpression, ParenthesizedExpression(left), chunkSize, intType);
							return true;
						}

						break;
					}
					case nameof(Enumerable.DefaultIfEmpty):
					{
						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.DefaultIfEmpty) ]).ToSet(), out currentSource);

						if (!TryOptimizeCollection(context, currentSource, out var resultInvocation))
						{
							resultInvocation = TryOptimizeByOptimizer<LongCountFunctionOptimizer>(context, CreateSimpleInvocation(currentSource, nameof(Enumerable.LongCount)));
						}

						result = CreateInvocation(ParseTypeName("Int64"), "Max", resultInvocation as ExpressionSyntax, CreateLiteral(1L));
						return true;
					}
					case nameof(Enumerable.Distinct):
					{
						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.Distinct) ]).ToSet(), out currentSource);

						result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(currentSource, nameof(Enumerable.Distinct)));
						result = CreateSimpleInvocation(result as ExpressionSyntax, nameof(Enumerable.LongCount));
						return true;
					}
					case nameof(Enumerable.Concat):
					{
						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount, out methodSource);

						var leftInvocation = UpdateInvocation(context, methodSource);
						var rightInvocation = CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters);

						var left = TryOptimizeByOptimizer<LongCountFunctionOptimizer>(context, leftInvocation) ?? leftInvocation;
						var right = TryOptimizeByOptimizer<LongCountFunctionOptimizer>(context, rightInvocation) ?? rightInvocation;

						var longType = context.Model.Compilation.CreateInt64();

						result = OptimizeArithmetic(context, SyntaxKind.AddExpression, context.Visit(left) ?? leftInvocation, context.Visit(right) ?? rightInvocation, longType);
						return true;
					}
				}
			}

			if (TryGetSyntaxes(currentSource, out var syntaxes))
			{
				result = CreateLiteral((long) syntaxes.Count);
				return true;
			}

			if (TryOptimizeCollection(context, currentSource, out result))
			{
				return true;
			}
		}

		if (IsEmptyEnumerable(currentSource))
		{
			result = CreateLiteral(0L);
			return true;
		}

		// If we skipped any operations, create optimized Count() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, currentSource);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeCollection(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result)
	{
		if (IsCollectionType(context, source))
		{
			result = CastExpression(ParseTypeName("long"), CreateMemberAccess(source, "Count"));
			return true;
		}

		if (IsInvokedOnArray(context, source))
		{
			result = CastExpression(ParseTypeName("long"), CreateMemberAccess(source, "Length"));
			return true;
		}

		result = null;
		return false;
	}
}