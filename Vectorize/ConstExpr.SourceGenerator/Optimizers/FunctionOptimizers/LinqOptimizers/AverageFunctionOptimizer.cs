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
///   Optimizer for Enumerable.Average context.Method.
///   Optimizes patterns such as:
///   - collection.AsEnumerable().Average() =&gt; collection.Average() (skip type cast)
///   - collection.ToList().Average() =&gt; collection.Average() (skip materialization)
///   - collection.ToArray().Average() =&gt; collection.Average() (skip materialization)
///   - collection.OrderBy(...).Average() =&gt; collection.Average() (ordering doesn't affect average)
///   - collection.Reverse().Average() =&gt; collection.Average() (reversing doesn't affect average)
/// </summary>
public class AverageFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Average), n => n is 0 or 1)
{
	// Operations that don't affect Average behavior
	private static readonly HashSet<string> OperationsThatDontAffectAverage =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Reverse)
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAverage, out source);

		if (IsEmptyEnumerable(source))
		{
			// Average of an empty sequence throws an exception, so we return a throw expression instead of optimizing to Average() which would be incorrect
			result = CreateThrowExpression<InvalidOperationException>("Sequence contains no elements");
			return true;
		}

		// check for x.Average(a => a) pattern and optimize to x.Average() since the selector is just the identity function and doesn't affect the average result
		if (context.VisitedParameters.Count > 0
		    && TryGetLambda(context.VisitedParameters[0], out var selector)
		    && IsIdentityLambda(selector))
		{
			if (IsIdentityLambda(selector))
			{
				result = UpdateInvocation(context, source, [ ]);
				return true;
			}

			if (TryVectorize(context, source, selector, CreateVectorizedMethod,
				    () => CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), source, context.Visit(selector) ?? selector),
				    () => CreateInvocation(source, "Exists", context.Visit(selector) ?? selector), out result))
			{
				return true;
			}
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
				                                    && TryGetLambda(selectorArg, out selector):
				{
					if (!TryVectorize(context, invocationSource, selector, CreateVectorizedMethod,
						    () => CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), invocationSource, context.Visit(selector) ?? selector),
						    () => CreateInvocation(invocationSource, "Exists", context.Visit(selector) ?? selector), out result))
					{
						result = UpdateInvocation(context, invocationSource, selector);
					}

					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();
					var doubleType = context.Model.Compilation.CreateDouble();

					// start + (count - 1) / 2.0
					var countMinusOne = ParenthesizedExpression(
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							countArg.Expression, CreateLiteral(1), intType));

					var halfOffset = OptimizeArithmetic(context, SyntaxKind.DivideExpression,
						countMinusOne, CreateLiteral(2.0), doubleType);

					var averageValue = OptimizeArithmetic(context, SyntaxKind.AddExpression,
						startArg.Expression, halfOffset, doubleType);

					// Guard against empty range (count == 0)
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType),
						averageValue,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					// Repeat(element, count).Average() => count > 0 ? (T)element : throw
					var castExpr = CastExpression(
						ParseName(context.Model.Compilation.GetMinimalString(context.Method.TypeArguments[0])),
						repeatElementArg.Expression);

					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
						castExpr,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
			}
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	private MethodDeclarationSyntax CreateVectorizedMethod(SyntaxNode vectorizedCode, LambdaExpressionSyntax lambda, FunctionOptimizerContext context)
	{
		var typeName = context.Model.Compilation.TryGetIEnumerableType(context.Method.ReceiverType, false, out var elementType) ? elementType : context.Method.TypeArguments[0];
		var returnTypeName = context.Method.ReturnType.ToDisplayString();

		var returnStatement = "sum / data.Length";

		if (IsEqualSymbol(context.Method.ReturnType, elementType))
		{
			returnStatement = $"({returnTypeName})sum / data.Length";
		}

		var result = $$"""
			private static {{returnTypeName}} {{Name}}(ReadOnlySpan<{{typeName}}> data)
			{
				if (Vector.IsHardwareAccelerated && data.Length >= Vector<{{typeName}}>.Count)
				{
					var vectors = MemoryMarshal.Cast<{{typeName}}, Vector<{{typeName}}>>(data);

					var acc0 = Vector<{{typeName}}>.Zero;
					var acc1 = Vector<{{typeName}}>.Zero;
					var acc2 = Vector<{{typeName}}>.Zero;
					var acc3 = Vector<{{typeName}}>.Zero;
					var i = 0;
					
					for (; i <= vectors.Length - 4; i += 4)
					{
						acc0 += {{ReplaceIdentifier(vectorizedCode, lambda, "vectors[i]")}};
						acc1 += {{ReplaceIdentifier(vectorizedCode, lambda, "vectors[i + 1]")}};
						acc2 += {{ReplaceIdentifier(vectorizedCode, lambda, "vectors[i + 2]")}};
						acc3 += {{ReplaceIdentifier(vectorizedCode, lambda, "vectors[i + 3]")}};
					}
					
					acc0 += acc1 + acc2 + acc3;
					
					for (; i < vectors.Length; i++)
					{
						acc0 += {{ReplaceIdentifier(vectorizedCode, lambda, "vectors[i]")}};
					}
					
					var sum = acc0.Sum();
					var tail = data.Length & Vector<{{typeName}}>.Count - 1;
					
					for (var t = data.Length - tail; t < data.Length; t++)
					{
						sum += {{ReplaceIdentifier(lambda.Body, lambda, "data[t]")}};
					}
					
					return {{returnStatement}};
				}
				
				var sum = {{elementType.GetDefaultValue()}};
				
				for (var i = 0; i < data.Length; i++)
				{
					sum += {{ReplaceIdentifier(lambda.Body, lambda, "data[i]")}};
				}
				
				return {{returnStatement}};
			}
			""";

		var method = ParseMemberDeclaration(result) as MethodDeclarationSyntax ?? throw new InvalidOperationException("Failed to parse vectorized method declaration");

		return method.WithIdentifier(Identifier($"{Name}_{method.Body.GetDeterministicHashString()}"));
	}
}