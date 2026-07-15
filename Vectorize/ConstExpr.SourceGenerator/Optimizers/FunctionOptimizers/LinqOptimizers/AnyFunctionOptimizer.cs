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
///   Optimizer for Enumerable.Any context.Method.
///   Optimizes patterns such as:
///   - collection.Where(predicate).Any() => collection.Any(predicate)
///   - collection.Select(...).Any() => collection.Any() (projection doesn't affect existence)
///   - collection.Distinct().Any() => collection.Any() (distinctness doesn't affect existence)
///   - collection.OrderBy(...).Any() => collection.Any() (ordering doesn't affect existence)
///   - collection.OrderByDescending(...).Any() => collection.Any() (ordering doesn't affect existence)
///   - collection.Order().Any() => collection.Any() (ordering doesn't affect existence)
///   - collection.OrderDescending().Any() => collection.Any() (ordering doesn't affect existence)
///   - collection.ThenBy(...).Any() => collection.Any() (secondary ordering doesn't affect existence)
///   - collection.ThenByDescending(...).Any() => collection.Any() (secondary ordering doesn't affect existence)
///   - collection.Reverse().Any() => collection.Any() (reversing doesn't affect existence)
///   - collection.AsEnumerable().Any() => collection.Any() (type cast doesn't affect existence)
///   - collection.ToList().Any() => collection.Any() (materialization doesn't affect existence)
///   - collection.ToArray().Any() => collection.Any() (materialization doesn't affect existence)
/// </summary>
public class AnyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Any), n => n is 0 or 1)
{
	// Operations that don't affect element existence (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectExistence =
	[
		.. MaterializingMethods,
		.. OrderingOperations,
		nameof(Enumerable.Select), // Projection: transforms elements but doesn't filter
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if any exist, Any() is true
		nameof(Enumerable.GroupBy), // Grouping: groups elements but doesn't filter them out, so it doesn't affect whether any elements exist
		"Chunk" // Chunking: groups elements but doesn't filter them out, so it doesn't affect whether any elements exist
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip all operations that don't affect existence
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectExistence, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Where) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
				                                   && TryGetLambda(predicateArg, out var predicate):
				{
					// Continue skipping operations before Where as well
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectExistence, out invocationSource);

					if (context.VisitedParameters.Count == 1 && TryGetLambda(context.VisitedParameters[0], out var anyPredicate))
					{
						predicate = CombinePredicates(predicate, anyPredicate, context);
					}

					if (IsSimpleEqualityLambda(predicate, out var equalityValue))
					{
						result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(invocationSource, nameof(Enumerable.Contains), equalityValue));
						return true;
					}

					return Vectorize(context, invocationSource, out result, context.Visit(predicate) as LambdaExpressionSyntax ?? predicate);
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectExistence, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var boolType = context.Model.Compilation.CreateBoolean();
					result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters), right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), boolType);
					return true;
				}
				case nameof(Enumerable.Append) or nameof(Enumerable.Prepend):
				{
					if (context.VisitedParameters.Count == 0)
					{
						result = CreateLiteral(true);
						return true;
					}

					if (context.VisitedParameters.Count == 1
					    && TryGetLambda(context.VisitedParameters[0], out var anyPredicate)
					    && TryGetLambdaBody(anyPredicate, out var anyPredicateBody)
					    && TryGetSimpleLambdaParameter(anyPredicate, out var anyPredicateParam)
					    && TryGetElementType(context, out var elementType))
					{
						var boolType = context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean);

						// collect all the append arguments in case of multiple appends in a chain, e.g. source.Append(x).Append(y).Any()
						var appendValues = new List<ExpressionSyntax> { context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, invocation.ArgumentList.Arguments[0].Expression)) };

						while (IsLinqMethodChain(invocationSource, out methodName, out var currentMethodInvocation)
						       && methodName is nameof(Enumerable.Append) or nameof(Enumerable.Prepend)
						       && TryGetLinqSource(currentMethodInvocation, out invocationSource))
						{
							if (currentMethodInvocation.ArgumentList.Arguments.Count == 0)
							{
								result = CreateLiteral(true);
								return true;
							}

							appendValues.Add(context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, currentMethodInvocation.ArgumentList.Arguments[0].Expression)));
						}

						var updatedInvocation = UpdateInvocation(context, invocationSource);

						appendValues.Add(TryOptimize(context.WithInvocationAndMethod(updatedInvocation, context.Method), out var rightResult) ? rightResult as ExpressionSyntax : updatedInvocation);

						result = appendValues.Skip(1).Aggregate(appendValues[0], (result, value)
							=> OptimizeComparison(context, SyntaxKind.LogicalOrExpression, result, value, boolType));

						return true;
					}

					break;
				}
				case nameof(Enumerable.DefaultIfEmpty):
				{
					if (context.VisitedParameters.Count == 0)
					{
						result = CreateLiteral(true);
						return true;
					}

					if (TryGetElementType(context, out var elementType))
					{
						var defaultValue = invocation.ArgumentList.Arguments.Count == 0
							? elementType.GetDefaultValue()
							: invocation.ArgumentList.Arguments[0].Expression;

						if (context.VisitedParameters.Count == 1
						    && TryGetLambda(context.VisitedParameters[0], out var anyPredicate)
						    && TryGetLambdaBody(anyPredicate, out var anyPredicateBody)
						    && TryGetSimpleLambdaParameter(anyPredicate, out var anyPredicateParam))
						{
							var boolType = context.Model.Compilation.CreateBoolean();
							var updatedInvocation = UpdateInvocation(context, invocationSource);

							var left = context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, defaultValue)) ?? defaultValue;
							var right = TryOptimize(context.WithInvocationAndMethod(updatedInvocation, context.Method), out var rightResult) ? rightResult as ExpressionSyntax : updatedInvocation;

							result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, left, right, boolType);
							return true;
						}
					}

					break;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();

						result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();

						// Repeat(element, count).Any() => count > 0
						result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), intType);
						return true;
					}

					break;
				}
			}
		}

		if (context.VisitedParameters.Count == 0)
		{
			var intType = context.Model.Compilation.CreateInt32();

			if (IsInvokedOnArray(context, source))
			{
				result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(source, "Length"),
					CreateLiteral(0), intType);

				return true;
			}

			if (IsCollectionType(context, source))
			{
				result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(source, "Count"),
					CreateLiteral(0), intType);

				return true;
			}
		}
		else if (TryGetLambda(context.VisitedParameters[0], out var anyLambda)
		         && IsSimpleEqualityLambda(anyLambda, out var equalityValue))
		{
			result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Contains), equalityValue));
			return true;
		}
		else if (context.VisitedParameters.Count == 1
		         && TryGetLambda(context.VisitedParameters[0], out anyLambda))
		{
			return Vectorize(context, source, out result, anyLambda);
		}

		// If we skipped any operations, create optimized Any() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	private bool Vectorize(FunctionOptimizerContext context, ExpressionSyntax source, out SyntaxNode? result, LambdaExpressionSyntax anyLambda)
	{
		if (IsInvocation(anyLambda, out var memberAccessBody)
		    && context.Model.Compilation
			    .GetTypeByMetadataName("System.Numerics.Tensors.TensorPrimitives")
			    .HasMethod($"{memberAccessBody.Name.Identifier.Text}Any"))
		{
			if (IsInvokedOnArray(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");

				result = CreateInvocation(ParseTypeName("TensorPrimitives"), $"{memberAccessBody.Name.Identifier.Text}Any", source);
				return true;
			}

			if (IsInvokedOnList(context, source))
			{
				context.Usings.Add("System.Numerics.Tensors");

				var spanSource = CreateInvocation(
					ParseTypeName("CollectionsMarshal"),
					"AsSpan",
					source);

				result = CreateInvocation(
					ParseTypeName("TensorPrimitives"),
					$"{memberAccessBody.Name.Identifier.Text}Any",
					spanSource);
				return true;
			}
		}

		if (!TryVectorize(context, source, anyLambda, CreateVectorizedMethod,
			    () => CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), source, anyLambda),
			    () => CreateInvocation(source, "Exists", anyLambda), out result))
		{
			result = UpdateInvocation(context, source, anyLambda);
		}

		return true;
	}

	private MethodDeclarationSyntax CreateVectorizedMethod(SyntaxNode vectorizedCode, LambdaExpressionSyntax lambda, FunctionOptimizerContext context)
	{
		var typeName = context.Method.TypeArguments[0].ToDisplayString();
		var ifStatement = $"Vector.AnyWhereAllBitsSet({ReplaceIdentifier(vectorizedCode, lambda, "vector")})";

		if (vectorizedCode is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
		{
			memberAccess = memberAccess.WithName(IdentifierName($"{memberAccess.Name.Identifier.Text}Any"));
			vectorizedCode = invocation.WithExpression(memberAccess);

			ifStatement = ReplaceIdentifier(vectorizedCode, lambda, "vector");
		}

		var result = $$"""
			private static bool Any(ReadOnlySpan<{{typeName}}> data)
			{
				var i = 0;
				var length = data.Length;

				if (Vector.IsHardwareAccelerated && length >= Vector<{{typeName}}>.Count)
				{
					ref var reference = ref MemoryMarshal.GetReference(data);

					do
					{
						var vector = Vector.LoadUnsafe(ref reference, (nuint)i);
					
						if ({{ifStatement}})
							return true;
							
						i += Vector<{{typeName}}>.Count;
					} while (i < length);
				}

				for (; i < length; i++)
				{
					if ({{ReplaceIdentifier(lambda.Body, lambda, "data[i]")}})
						return true;
				}

				return false;
			}
			""";

		var method = ParseMemberDeclaration(result) as MethodDeclarationSyntax ?? throw new InvalidOperationException("Failed to parse vectorized method declaration");

		return method.WithIdentifier(Identifier($"{Name}_{method.Body.GetDeterministicHashString()}"));
	}
}