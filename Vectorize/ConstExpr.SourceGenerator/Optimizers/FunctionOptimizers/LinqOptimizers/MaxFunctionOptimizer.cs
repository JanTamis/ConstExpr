using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MathMaxOptimizer = ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MaxFunctionOptimizer;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
///   Optimizer for Enumerable.Max context.Method.
///   Optimizes patterns such as:
///   - collection.Max(x => x) => collection.Max() (identity lambda removal)
///   - collection.Select(x => x.Property).Max() => collection.Max(x => x.Property)
///   - collection.OrderBy(...).Max() => collection.Max() (ordering doesn't affect max)
///   - collection.AsEnumerable().Max() => collection.Max()
///   - collection.ToList().Max() => collection.Max()
/// </summary>
public class MaxFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Max), n => n is 0 or 1)
{
	// Operations that don't affect the maximum value
	private static readonly HashSet<string> OperationsThatDontAffectMax =
	[
		.. MaterializingMethods,
		.. OrderingOperations,
		nameof(Enumerable.Reverse)
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip operations that don't affect max
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMax, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var hasTensorPrimitivesMax = context.Model.Compilation
			.GetTypeByMetadataName("System.Numerics.Tensors.TensorPrimitives")
			.HasMethod("Max");

		// Optimize Max(x => x) => Max() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			if (hasTensorPrimitivesMax && IsInvokedOnArray(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");
				result = CreateInvocation(ParseTypeName("TensorPrimitives"), "Max", source);
				return true;
			}

			if (hasTensorPrimitivesMax && IsInvokedOnList(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");
				context.Usings.Add("System.Runtime.InteropServices");
				result = CreateInvocation(ParseTypeName("TensorPrimitives"), "Max", CreateInvocation(ParseTypeName("CollectionsMarshal"), "AsSpan", source));
				return true;
			}

			result = UpdateInvocation(context, source, [ ]);
			return true;
		}

		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize source.Select(selector).Max() => source.Max(selector)
				case nameof(Enumerable.Select) when invocation.ArgumentList.Arguments.Count == 1:
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var selector = invocation.ArgumentList.Arguments[0].Expression;

					result = UpdateInvocation(context, invocationSource, selector);
					return true;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var concatArg = invocation.ArgumentList.Arguments[0].Expression;
					var visitedConcatArg = context.Visit(concatArg) ?? concatArg;

					var leftInvocation = UpdateInvocation(context, invocationSource);
					var rightInvocation = CreateInvocation(visitedConcatArg, Name, context.VisitedParameters);

					var left = TryOptimizeByOptimizer<MaxFunctionOptimizer>(context, leftInvocation);
					var right = TryOptimizeByOptimizer<MaxFunctionOptimizer>(context, rightInvocation);

					result = OptimizeAsMathPairwise<MathMaxOptimizer>(context, context.Visit(left) ?? leftInvocation, context.Visit(right) ?? rightInvocation);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();

					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType),
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, countArg.Expression, intType),
							CreateLiteral(1), intType),
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
						repeatElementArg.Expression,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
			}
		}

		// TensorPrimitives.Max for plain Max() on arrays/lists (no selector, no constant source)
		if (context.VisitedParameters.Count == 0 && hasTensorPrimitivesMax)
		{
			if (IsInvokedOnArray(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");
				result = CreateInvocation(ParseTypeName("TensorPrimitives"), "Max", source);
				return true;
			}

			if (IsInvokedOnList(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");
				context.Usings.Add("System.Runtime.InteropServices");
				result = CreateInvocation(ParseTypeName("TensorPrimitives"), "Max", CreateInvocation(ParseTypeName("CollectionsMarshal"), "AsSpan", source));
				return true;
			}
		}

		// If we skipped any operations, create optimized Max() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}