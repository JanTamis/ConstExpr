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
/// </summary>
public class CountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Count), 0, 1)
{
	// Operations that don't affect element count (only order/form but not filtering)
	// Note: We DON'T include Distinct, ToList, ToArray because they might affect count
	// - Distinct: reduces count by removing duplicates
	// - ToList/ToArray: materialization could fail/filter
	private static readonly HashSet<string> OperationsThatDontAffectCount =
	[
		nameof(Enumerable.OrderBy),          // Ordering: changes order but not count
		nameof(Enumerable.OrderByDescending),// Ordering: changes order but not count
		"Order",                             // Ordering (.NET 6+): changes order but not count
		"OrderDescending",                   // Ordering (.NET 6+): changes order but not count
		nameof(Enumerable.ThenBy),           // Secondary ordering: changes order but not count
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not count
		nameof(Enumerable.Reverse),          // Reversal: changes order but not count
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.Select),					 // Projection: doesn't change count for non-nullable types
		nameof(Enumerable.ToList),           // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray),          // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax>();
		var currentSource = source;

		// Walk through the chain and collect all Where statements
		while (IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out var whereInvocation)
		       && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		       && TryGetLambda(predicateArg, out var predicate)
		       && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			if (IsLiteralBooleanLambda(predicate, out var literalValue) && literalValue == true)
			{
				switch (literalValue)
				{
					case true:
						TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out currentSource);
						continue;
					case false:
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
						return true;
				}
			}
			
			wherePredicates.Add(predicate);

			currentSource = whereSource;
			
			// Skip operations that don't affect count before the next Where
			isNewSource = TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectCount, out currentSource) || isNewSource;
		}

		// If we found any Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// try to execute predicates directly if we can get the values at compile time (e.g. for arrays or collections with known contents)
			if (TryGetValues(context.Visit(currentSource) ?? currentSource, out var values))
			{
				var lambdas = wherePredicates
					.Select(s => context.GetLambda(s)?.Compile())
					.ToList();
				
				var count = values.Count(value => lambdas.All(lambda => lambda?.DynamicInvoke(value) is true));
				
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(count));
				return true;
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
						result = CreateMemberAccess(context.Visit(currentSource) ?? currentSource, "Count");
						return true;
					case true when IsInvokedOnArray(context, currentSource):
						result = CreateMemberAccess(context.Visit(currentSource) ?? currentSource, "Length");
						return true;
					case false:
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
						return true;
				}
			}

			if (IsLinqMethodChain(currentSource, nameof(Enumerable.DefaultIfEmpty), out var chunkInvocation))
			{
				TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.DefaultIfEmpty) ]).ToSet(), out currentSource);
				
				result = TryOptimizeByOptimizer<CountFunctionOptimizer>(context, CreateInvocation(currentSource, nameof(Enumerable.Count), combinedPredicate));
				result = CreateInvocation(SyntaxFactory.ParseTypeName("Int32"), "Max", result as ExpressionSyntax, SyntaxHelpers.CreateLiteral(1)!);
				return true;
			}
			
			currentSource = context.Visit(currentSource) ?? currentSource;

			if (IsEmptyEnumerable(currentSource))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
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
					case "Chunk" when invocation.ArgumentList.Arguments is [var chunkSizeArg]:
					{
						var chunkSize = context.Visit(chunkSizeArg.Expression) ?? chunkSizeArg.Expression;

						if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
						{
							currentSource = methodSource;
						}
						else
						{
							var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int32);
							var newChunkSource = context.Visit(methodSource) ?? methodSource;

							if (!TryOptimizeCollection(context, newChunkSource, out var countInvocation))
							{
								countInvocation = CreateSimpleInvocation(newChunkSource, nameof(Enumerable.Count));
							}

							var left = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression,
								countInvocation as ExpressionSyntax,
								context.OptimizeBinaryExpression(SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, chunkSize, SyntaxHelpers.CreateLiteral(1)!), intType, intType, intType) as ExpressionSyntax ?? chunkSize);

							result = context.OptimizeBinaryExpression(SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression, SyntaxFactory.ParenthesizedExpression(context.OptimizeBinaryExpression(left, intType, intType, intType) as ExpressionSyntax ?? left), chunkSize), intType, intType, intType) as ExpressionSyntax ?? chunkSize;
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
						
						result = CreateInvocation(SyntaxFactory.ParseTypeName("Int32"), "Max", resultInvocation as ExpressionSyntax, SyntaxHelpers.CreateLiteral(1)!);
						return true;
					}
					case nameof(Enumerable.Distinct):
					{
						TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectCount.Union([ nameof(Enumerable.Distinct) ]).ToSet(), out currentSource);
						
						result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(currentSource, nameof(Enumerable.Distinct)));
						result = CreateSimpleInvocation(result as ExpressionSyntax, nameof(Enumerable.Count));
						return true;
					}
				}
			}
			
			if (TryGetSyntaxes(context.Visit(currentSource) ?? currentSource, out var syntaxes))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(syntaxes.Count));
				return true;
			}

			if (TryOptimizeCollection(context, currentSource, out result)
			    || TryOptimizeCollection(context, source, out result))
			{
				return true;
			}
		}

		source = context.Visit(currentSource) ?? currentSource;

		if (TryOptimizeCollection(context, source, out result))
		{
			return true;
		}

		if (IsEmptyEnumerable(source))
		{
			result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
			return true;
		}

		// If we skipped any operations, create optimized Count() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
	
	private bool TryOptimizeCollection(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result)
	{
		if (IsCollectionType(context, source))
		{
			result = CreateMemberAccess(context.Visit(source) ?? source, "Count");
			return true;
		}

		if (IsInvokedOnArray(context, source))
		{
			result = CreateMemberAccess(context.Visit(source) ?? source, "Length");
			return true;
		}
		
		result = null;
		return false;
	}
}

