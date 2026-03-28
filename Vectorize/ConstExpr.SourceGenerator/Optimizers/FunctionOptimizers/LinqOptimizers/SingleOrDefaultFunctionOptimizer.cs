using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SingleOrDefault context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).SingleOrDefault() => collection.SingleOrDefault(predicate)
/// - collection.AsEnumerable().SingleOrDefault() => collection.SingleOrDefault()
/// - collection.ToList().SingleOrDefault() => collection.SingleOrDefault()
/// </summary>
public class SingleOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SingleOrDefault), 0, 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip operations that don't affect singleOrDefault
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize source.Where(predicate).SingleOrDefault() => source.SingleOrDefault(predicate)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && TryGetLinqSource(whereInvocation, out var whereSource)
		    && whereInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(whereSource, MaterializingMethods, out whereSource);

			var predicate = whereInvocation.ArgumentList.Arguments[0].Expression;
			var tempSource = context.Visit(whereSource) ?? whereSource;

			if (TryGetValues(tempSource, out var values))
			{
				var lambda = context.GetLambda(predicate as LambdaExpressionSyntax);

				if (lambda != null)
				{
					var compiledPredicate = lambda.Compile();

					// If we can evaluate the predicate at compile time, we can determine if SingleOrDefault will return a constant value
					var matchingValues = values.Where(v => compiledPredicate.DynamicInvoke(v) is true).ToList();

					switch (matchingValues.Count)
					{
						case 0:
							// No matching elements, SingleOrDefault will return default(T)
							result = context.Method.TypeArguments[0].GetDefaultValue();
							return true;
						case > 1:
							// More than one matching element, SingleOrDefault will throw
							result = CreateThrowExpression<InvalidOperationException>("Sequence contains more than one matching element");
							return true;
						case 1
							when TryCreateLiteral(matchingValues[0], out var literal):
							result = literal;
							return true;
					}
				}
			}

			result = UpdateInvocation(context, whereSource, context.Visit(predicate) ?? predicate);
			return true;
		}

		// If we skipped any operations, create optimized SingleOrDefault() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}