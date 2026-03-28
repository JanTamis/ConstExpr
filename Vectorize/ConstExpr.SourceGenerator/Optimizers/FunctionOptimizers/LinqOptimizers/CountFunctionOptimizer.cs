using System;
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
/// Optimizer for Enumerable.Count context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Count() => collection.Count(predicate)
/// - collection.Where(p1).Where(p2).Count() => collection.Count(p1 && p2) (multiple chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3).Count() => collection.Count(p1 && p2 && p3)
/// - collection.Select(...).Count() => collection.Count() (projection doesn't affect count for non-null elements)
/// - collection.OrderBy(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderByDescending(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.Order().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderDescending().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.ThenBy(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.ThenByDescending(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.Reverse().Count() => collection.Count() (reversing doesn't affect count)
/// - collection.AsEnumerable().Count() => collection.Count() (type cast doesn't affect count)
/// - collection.OrderBy(...).Where(p1).Where(p2).Count() => collection.Count(p1 && p2) (combining operations)
/// - collection.Take(n).Count() => Int32.Min(n, collection.Count()) (take limits count)
/// - collection.Skip(n).Count() => Int32.Max(0, collection.Count() - n) (skip reduces count)
/// </summary>
public class CountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Count), 0, 1)
{
	// Operations that don't affect element count (only order/form but not filtering)
	// Note: We DON'T include Distinct, ToList, ToArray because they might affect count
	// - Distinct: reduces count by removing duplicates
	// - ToList/ToArray: materialization could fail/filter
	private static readonly HashSet<string> OperationsThatDontAffectCount =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Select),
		"Index",
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax>();

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);

		if (TryExecutePredicates(context, source, out result, out _))
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
						result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
						return true;
				}
			}

			wherePredicates.Add(predicate);

			source = whereSource;

			// Skip operations that don't affect count before the next Where
			isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source) || isNewSource;
		}

		var currentSource = context.Visit(source) ?? source;

		// If visiting the source transformed it (e.g., Intersect → Distinct+Where),
		// re-run the Count optimization on the new expression to fold Where+Count into Count(predicate)
		if (wherePredicates.Count == 0
		    && !AreSyntacticallyEquivalent(currentSource, source)
		    && IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out _))
		{
			var countInvocation = CreateInvocation(currentSource, nameof(Enumerable.Count), context.VisitedParameters);
			result = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, countInvocation);
			return true;
		}

		// If we found any Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// try to execute predicates directly if we can get the values at compile time (e.g. for arrays or collections with known contents)
			if (TryGetValues(currentSource, out var values))
			{
				var lambdas = wherePredicates
					.WhereSelect<LambdaExpressionSyntax, object>((s, out result) => TryGetLiteralValue(s, context, null, out result))
					.OfType<Delegate>()
					.ToList();

				if (lambdas.Count == wherePredicates.Count)
				{
					var count = values.Count(value => lambdas.All(lambda => lambda?.DynamicInvoke(value) is true));

					result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(count));
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
				combinedPredicate = CombinePredicates(context.Visit(lambda) as LambdaExpressionSyntax ?? lambda, combinedPredicate);
			}

			combinedPredicate = context.Visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true when IsCollectionType(context, currentSource):
						result = CreateMemberAccess(currentSource, "Count");
						return true;
					case true when IsInvokedOnArray(context, currentSource):
						result = CreateMemberAccess(currentSource, "Length");
						return true;
					case false:
						result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
						return true;
				}
			}

			if (IsLinqMethodChain(currentSource, nameof(Enumerable.DefaultIfEmpty), out _))
			{
				TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.DefaultIfEmpty) ]).ToSet(), out currentSource);

				result = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, CreateInvocation(currentSource, nameof(Enumerable.Count), combinedPredicate));
				result = CreateInvocation(ParseTypeName("Int32"), "Max", result as ExpressionSyntax, CreateLiteral(1));
				return true;
			}

			if (IsEmptyEnumerable(currentSource))
			{
				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
				return true;
			}

			result = UpdateInvocation(context, currentSource, combinedPredicate);
			return true;
		}

		isNewSource = TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectCount, out currentSource) || isNewSource;

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
							var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int32);
							var newChunkSource = methodSource;

							if (!TryOptimizeCollection(context, newChunkSource, out var countInvocation))
							{
								countInvocation = CreateSimpleInvocation(newChunkSource, nameof(Enumerable.Count));
							}

							var chunkMinus1 = OptimizeArithmetic(context, SyntaxKind.SubtractExpression, chunkSize, CreateLiteral(1), intType);
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
							resultInvocation = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, CreateSimpleInvocation(currentSource, nameof(Enumerable.Count)));
						}

						result = CreateInvocation(ParseTypeName("Int32"), "Max", context.Visit(resultInvocation), CreateLiteral(1));
						return true;
					}
					case nameof(Enumerable.Distinct):
					{
						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.Distinct) ]).ToSet(), out currentSource);

						result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(currentSource, nameof(Enumerable.Distinct)));
						result = CreateSimpleInvocation(result as ExpressionSyntax, nameof(Enumerable.Count));
						return true;
					}
					case nameof(Enumerable.SelectMany) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					                                        && TryGetLambda(predicateArg, out var predicate):
					{
						var body = predicate.Body as ExpressionSyntax;

						var countInvocation = CreateInvocation(body, nameof(Enumerable.Count), context.VisitedParameters);
						var newCountInvocation = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, countInvocation) as ExpressionSyntax ?? countInvocation;

						predicate = predicate.WithBody(newCountInvocation);

						var sumInvocation = CreateInvocation(methodSource, nameof(Enumerable.Sum), predicate);

						result = TryOptimizeByOptimizer<SumFunctionOptimizer>(context, sumInvocation);
						return true;
					}
					case nameof(Enumerable.Append) or nameof(Enumerable.Prepend):
					{
						var count = 1;

						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount, out currentSource);

						while (IsLinqMethodChain(currentSource, out var innerMethodName, out var appendInvocation)
						       && innerMethodName is nameof(Enumerable.Append) or nameof(Enumerable.Prepend)
						       && TryGetLinqSource(appendInvocation, out var appendSource))
						{
							count++;

							TryGetOptimizedChainExpression(appendSource, OperationsThatDontAffectCount, out currentSource);
						}

						if (TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, currentSource), context.Method), out result))
						{
							var intType2 = context.Model.Compilation.CreateInt32();
							result = OptimizeArithmetic(context, SyntaxKind.AddExpression, result as ExpressionSyntax, CreateLiteral(count), intType2);
							return true;
						}

						break;
					}
					case nameof(Enumerable.Concat):
					{
						if (TryGetSyntaxes(invocation.ArgumentList.Arguments[0].Expression, out var concatSyntaxes))
						{
							var count = concatSyntaxes.Count;

							TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount, out currentSource);

							while (IsLinqMethodChain(currentSource, nameof(Enumerable.Concat), out var concatInvocation)
							       && TryGetLinqSource(concatInvocation, out var concatSource)
							       && TryGetSyntaxes(concatInvocation.ArgumentList.Arguments[0].Expression, out concatSyntaxes))
							{
								count += concatSyntaxes.Count;

								TryGetOptimizedChainExpression(concatSource, OperationsThatDontAffectCount, out currentSource);
							}

							if (TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, currentSource), context.Method), out result))
							{
								var intType2 = context.Model.Compilation.CreateInt32();
								result = OptimizeArithmetic(context, SyntaxKind.AddExpression, result as ExpressionSyntax, CreateLiteral(count), intType2);
								return true;
							}
						}
						else
						{
							TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount, out methodSource);

							var leftInvocation = UpdateInvocation(context, methodSource);
							var rightInvocation = CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters);

							var left = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, leftInvocation) ?? leftInvocation;
							var right = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, rightInvocation) ?? rightInvocation;

							var intType = context.Model.Compilation.CreateInt32();

							result = OptimizeArithmetic(context, SyntaxKind.AddExpression, context.Visit(left) ?? leftInvocation, context.Visit(right) ?? rightInvocation, intType);
							return true;
						}

						break;
					}
					case nameof(Enumerable.Zip) when invocation.ArgumentList.Arguments.Count == 1:
					{
						var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, methodSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
						var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

						result = CreateInvocation(context.Model.Compilation.CreateInt32(), "Min",
							context.Visit(left) ?? CreateInvocation(currentSource, Name, context.VisitedParameters),
							context.Visit(right) ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters));
						return true;
					}
					case nameof(Enumerable.Take) when invocation.ArgumentList.Arguments is [ var takeArg ]:
					{
						var takeAmount = takeArg.Expression;

						// Resolve source.Count() as optimally as possible
						var sourceCount = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, methodSource), context.Method), out var countResult)
							? countResult as ExpressionSyntax ?? CreateSimpleInvocation(methodSource, nameof(Enumerable.Count))
							: CreateSimpleInvocation(methodSource, nameof(Enumerable.Count));

						result = CreateInvocation(context.Model.Compilation.CreateInt32(), "Min", takeAmount, sourceCount);
						return true;
					}
					case nameof(Enumerable.Skip) when invocation.ArgumentList.Arguments is [ var skipArg ]:
					{
						var skipAmount = skipArg.Expression;
						var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int32);

						// Resolve source.Count() as optimally as possible
						var sourceCount = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, methodSource), context.Method), out var countResult)
							? countResult as ExpressionSyntax ?? CreateSimpleInvocation(methodSource, nameof(Enumerable.Count))
							: CreateSimpleInvocation(methodSource, nameof(Enumerable.Count));

						// source.Count() - skipAmount  (fold to literal when both are constant)
						var subtracted = OptimizeArithmetic(context, SyntaxKind.SubtractExpression, sourceCount, skipAmount, intType);

						result = CreateInvocation(context.Model.Compilation.CreateInt32(), "Max", CreateLiteral(0), subtracted);
						return true;
					}
					case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
					{
						result = countArg.Expression;
						return true;
					}
					case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var elementArg, var repeatCountArg ]:
					{
						// Repeat(element, count).Count() => count
						result = repeatCountArg.Expression;
						return true;
					}
					case "CountBy" when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					                    && TryGetLambda(predicateArg, out var predicate):
					{
						if (IsIdentityLambda(predicate))
						{
							result = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Count)));
							return true;
						}

						if (context.Model.TryGetSymbol<IMethodSymbol>(invocation, out var methodSymbol))
						{
							var distinctByInvocation = CreateInvocation(methodSource, "DistinctBy", predicate);
							var newDistinctByCountInvocation = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, distinctByInvocation, methodSymbol.TypeArguments.ToArray()) as ExpressionSyntax ?? distinctByInvocation;

							result = UpdateInvocation(context, newDistinctByCountInvocation);
							return true;

						}

						break;
					}
				}
			}

			if (TryGetSyntaxes(currentSource, out var syntaxes))
			{
				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(syntaxes.Count));
				return true;
			}

			if (TryOptimizeCollection(context, currentSource, out result))
			{
				return true;
			}
		}

		if (IsLinqMethodChain(currentSource, out var chainMethodName, out var chainInvocation)
		    && TryGetLinqSource(chainInvocation, out var chainSource))
		{
			switch (chainMethodName)
			{
				case "CountBy" when GetMethodArguments(chainInvocation).FirstOrDefault() is { Expression: { } predicateArg }
				                    && TryGetLambda(predicateArg, out var predicate):
				{
					if (IsIdentityLambda(predicate))
					{
						var distinctInvocation = CreateSimpleInvocation(chainSource, nameof(Enumerable.Distinct));
						var newCountInvocation = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, distinctInvocation) as ExpressionSyntax ?? distinctInvocation;

						result = UpdateInvocation(context, newCountInvocation);
						return true;
					}

					if (context.Model.TryGetSymbol<IMethodSymbol>(currentSource, out var methodSymbol))
					{
						var distinctByInvocation = CreateInvocation(chainSource, "DistinctBy", predicate);
						var newDistinctByCountInvocation = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, distinctByInvocation, methodSymbol.TypeArguments.ToArray()) as ExpressionSyntax ?? distinctByInvocation;

						result = UpdateInvocation(context, newDistinctByCountInvocation);
						return true;
					}

					break;
				}

				case nameof(Enumerable.GroupBy) when GetMethodArguments(chainInvocation).FirstOrDefault() is { Expression: { } predicateArg }
				                                     && TryGetLambda(predicateArg, out var predicate):
				{
					var distinctByInvocation = CreateInvocation(chainSource, "DistinctBy", predicate);
					var newDistinctByCountInvocation = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, distinctByInvocation) as ExpressionSyntax ?? distinctByInvocation;

					result = UpdateInvocation(context, newDistinctByCountInvocation);
					return true;
				}
			}

			if (IsEmptyEnumerable(currentSource))
			{
				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
				return true;
			}

			// If we skipped any operations, create optimized Count() call
			if (isNewSource
			    || !AreSyntacticallyEquivalent(currentSource, source))
			{
				result = UpdateInvocation(context, currentSource);
				return true;
			}
		}

		result = null;
		return false;
	}

	private bool TryOptimizeCollection(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result)
	{
		if (IsInvokedOnArray(context, source))
		{
			result = CreateMemberAccess(source, "Length");
			return true;
		}

		if (IsCollectionType(context, source))
		{
			result = CreateMemberAccess(source, "Count");
			return true;
		}

		result = null;
		return false;
	}
}