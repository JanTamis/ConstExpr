using System;
using System.Collections;
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
/// Optimizer for Enumerable.GroupBy context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().GroupBy(selector) => Enumerable.Empty&lt;IGrouping&lt;TKey, T&gt;&gt;()
/// </summary>
public class GroupByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.GroupBy), n => n is 1 or 2 or 3)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryGenerateGroupingStruct(context, source, out result))
		{
			return true;
		}

		// if (TryExecutePredicates(context, source, out result, out source))
		// {
		// 	return true;
		// }

		// Optimize Enumerable.Empty<T>().GroupBy(selector) => Enumerable.Empty<IGrouping<TKey, T>>()
		if (IsEmptyEnumerable(source) && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Tries to compute the lookup at compile time and generate a custom ILookup struct.
	/// </summary>
	private bool TryGenerateGroupingStruct(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		try
		{
			var visitedSource = context.Visit(source) ?? source;

			if (context.OriginalParameters.Count != context.Method.Parameters.Length
			    || context.Method.ReceiverType is not INamedTypeSymbol receiverType
			    || !TryGetLiteralValue(visitedSource, context, receiverType, out var values)
			    || !context.Loader.TryGetMethodByMethod(context.Method, out var method))
			{
				return false;
			}

			var parameters = new List<object?>();

			for (var i = 0; i < context.OriginalParameters.Count; i++)
			{
				if (TryGetLiteralValue(context.OriginalParameters[i], context, context.Method.Parameters[i].Type, out var value)
				    || TryGetLiteralValue(context.VisitedParameters[i], context, context.Method.Parameters[i].Type, out value))
				{
					parameters.Add(value);
				}
			}

			if (parameters.Count != context.Method.Parameters.Length)
			{
				return false;
			}

			// Invoke ToLookup via reflection to get the actual ILookup result
			object? lookupResult;

			if (method.IsStatic)
			{
				lookupResult = method.Invoke(null, [ values, ..parameters ]);
			}
			else
			{
				lookupResult = method.Invoke(values, [ ..parameters ]);
			}

			if (lookupResult is null)
			{
				return false;
			}

			// Extract TKey and TElement from the return type ILookup<TKey, TElement>
			if (context.Method.Arity != 2)
			{
				return false;
			}

			var keyTypeSymbol = context.Method.TypeArguments[0];
			var elementTypeSymbol = context.Method.TypeArguments[1];

			var keyTypeName = keyTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
			var elementTypeName = elementTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

			// Extract groups from the ILookup via reflection
			var groups = ExtractGroups(lookupResult);

			if (groups is null)
			{
				return false;
			}

			if (groups.Count > 0)
			{
				// Generate the Grouping helper struct (shared across all lookups)
				var groupingStruct = ParseTypeFromString<StructDeclarationSyntax>("""
					file readonly struct Grouping<TKey, TElement>(TKey key, params IEnumerable<TElement> elements) : IGrouping<TKey, TElement>
					{
						public TKey Key => key;

						public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();
						IEnumerator IEnumerable.GetEnumerator() => elements.GetEnumerator();
					}
					""");

				context.AdditionalSyntax.TryAdd(groupingStruct, true);

				// Generate an collection expression of Grouping<TKey, TElement> instances for each group
				var groupExpressions = groups.Select(g =>
				{
					var args = new List<SyntaxNodeOrToken>
					{
						Argument(CreateLiteral(g.Key))
					};

					foreach (var element in g.Elements)
					{
						args.Add(Token(SyntaxKind.CommaToken));
						args.Add(Argument(CreateLiteral(element)));
					}

					return ObjectCreationExpression(
							ParseTypeName($"Grouping<{keyTypeName}, {elementTypeName}>"))
						.WithArgumentList(
							ArgumentList(
								SeparatedList<ArgumentSyntax>(args)));
				});

				result = CreateCollection(groupExpressions);
			}
			else
			{
				// If there are no groups, we can just return an empty array (empty lookup)
				result = CreateEmptyEnumerableCall(context.Method.ReturnType);
			}

			return true;
		}
		catch (Exception e)
		{
			return false;
		}
	}

	/// <summary>
	/// Extracts groups from an ILookup object using reflection.
	/// Returns a list of (key, elements[]) tuples.
	/// </summary>
	private static List<(object? Key, List<object?> Elements)>? ExtractGroups(object lookupResult)
	{
		var groups = new List<(object? Key, List<object?> Elements)>();

		if (lookupResult is not IEnumerable enumerable)
		{
			return null;
		}

		foreach (var grouping in enumerable)
		{
			var groupType = grouping.GetType();
			var keyProp = groupType.GetProperty("Key");

			if (keyProp is null)
			{
				return null;
			}

			var key = keyProp.GetValue(grouping);
			var elements = new List<object?>();

			if (grouping is IEnumerable elementEnumerable)
			{
				foreach (var element in elementEnumerable)
				{
					elements.Add(element);
				}
			}

			groups.Add((key, elements));
		}

		return groups;
	}
}