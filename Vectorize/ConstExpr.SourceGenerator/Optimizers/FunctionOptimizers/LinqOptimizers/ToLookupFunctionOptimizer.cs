using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToLookup method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.ToList().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.ToArray().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.OrderBy(...).ToLookup(keySelector) => collection.ToLookup(keySelector) (ordering doesn't affect lookup)
/// - collection.OrderByDescending(...).ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.Reverse().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.Select(x => x).ToLookup(keySelector) => collection.ToLookup(keySelector) (identity Select is a no-op)
/// - collection.ToLookup(keySelector, x => x) => collection.ToLookup(keySelector) (identity element-selector)
/// - Enumerable.Empty&lt;T&gt;().ToLookup(keySelector) => Enumerable.Empty&lt;T&gt;().ToLookup(keySelector) (no further optimization possible for empty)
/// - collection.Select(selector).ToLookup(keySelector) => collection.ToLookup(x => keySelector(selector(x)), selector) (fold Select into ToLookup)
/// - collection.Where(p1).Where(p2).ToLookup(keySelector) => collection.Where(p1 &amp;&amp; p2).ToLookup(keySelector) (merge chained Where predicates)
/// - When source values are compile-time known, generates a custom ILookup&lt;TKey, TElement&gt; struct implementation.
/// Note: Unlike ToDictionary, Distinct is NOT redundant before ToLookup because ToLookup groups
/// duplicate keys rather than throwing, so removing Distinct could change group sizes.
/// </summary>
public class ToLookupFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToLookup), 1, 2, 3)
{
	// Operations that don't affect the content of the resulting lookup
	// Note: We do NOT include Distinct here because ToLookup groups duplicates —
	// removing Distinct would change the number of elements within each group.
	private static readonly HashSet<string> OperationsThatDontAffectLookup =
	[
		..MaterializingMethods,
		..OrderingOperations,
	];

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectLookup, out source);

		// if (TryExecutePredicates(context, source, out result, out source))
		// {
		// 	return true;
		// }

		// Try to generate a compile-time custom ILookup struct when source values are known
		if (TryGenerateLookupStruct(context, source, out result))
		{
			return true;
		}

		// Optimize ToLookup(keySelector, x => x) => ToLookup(keySelector) when element-selector is identity
		if (context.VisitedParameters.Count == 2
		    && TryGetLambda(context.VisitedParameters[1], out var elementSelectorLambda)
		    && IsIdentityLambda(elementSelectorLambda))
		{
			result = CreateInvocation(source, nameof(Enumerable.ToLookup), context.VisitedParameters[0]);
			return true;
		}

		// Walk the chain for Select / Where optimizations
		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize Select(selector).ToLookup(keySelector) =>
				//   ToLookup(x => keySelector(selector(x)), selector)
				// When ToLookup has only a keySelector (1 param), fold the Select projection
				// into both the keySelector and a new elementSelector.
				case nameof(Enumerable.Select)
					when context.VisitedParameters.Count == 1
					     && GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
					     && TryGetLambda(selectorArg, out var selectLambda)
					     && TryGetLambda(context.VisitedParameters[0], out var keyLambda):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectLookup, out invocationSource);

					// Compose keySelector ∘ selectLambda
					var composedKey = CombineLambdas(keyLambda, selectLambda);

					// If the Select lambda is identity, the element-selector would also be identity → drop it
					if (IsIdentityLambda(selectLambda))
					{
						result = CreateInvocation(invocationSource, nameof(Enumerable.ToLookup), composedKey);
					}
					else
					{
						// Select(selector).ToLookup(keySelector) => ToLookup(composedKey, selector)
						result = UpdateInvocation(context, invocationSource, composedKey, selectorArg);
					}

					return true;
				}
			}
		}

		// Strip redundant operations (e.g. .ToList().ToLookup() => .ToLookup())
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Tries to compute the lookup at compile time and generate a custom ILookup struct.
	/// </summary>
	private bool TryGenerateLookupStruct(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
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
			if (context.Method.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 2 } returnType)
			{
				return false;
			}

			var keyTypeSymbol = returnType.TypeArguments[0];
			var elementTypeSymbol = returnType.TypeArguments[1];

			var keyTypeName = keyTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
			var elementTypeName = elementTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

			// Extract groups from the ILookup via reflection
			var groups = ExtractGroups(lookupResult);

			if (groups is null)
			{
				return false;
			}

			var hash = new HashCode();

			// Also hash the actual groups content so the struct name reflects the computed lookup data
			foreach (var (key, elements) in groups)
			{
				hash.Add(FormatLiteral(key));

				foreach (var element in elements)
				{
					hash.Add(FormatLiteral(element));
				}
			}

			// Generate a unique struct name based on the content
			var structName = $"Lookup_{CompilationExtensions.GetDeterministicHashString(hash.ToHashCode())}";

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

				context.AdditionalMethods.TryAdd(groupingStruct, true);
			}

			// Build the Contains body as a `key is x or y or z` pattern expression and run through context.Visit
			var containsExpression = BuildContainsPatternExpression(groups, context);

			// Build the lookup struct source
			var lookupStructSource = BuildLookupStructSource(structName, keyTypeName, elementTypeName, groups, containsExpression);
			var lookupStruct = ParseTypeFromString<StructDeclarationSyntax>(lookupStructSource);

			context.AdditionalMethods.TryAdd(lookupStruct, true);

			// Return new StructName() as the replacement expression
			result = SyntaxFactory.ObjectCreationExpression(
					SyntaxFactory.IdentifierName(structName))
				.WithArgumentList(SyntaxFactory.ArgumentList());

			return true;
		}
		catch
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

	/// <summary>
	/// Builds a <c>key is x or y or z</c> pattern expression for the Contains method,
	/// then runs it through <see cref="FunctionOptimizerContext.Visit"/> so the optimizer
	/// can further simplify it (e.g. contiguous integer ranges become a bit-trick).
	/// </summary>
	private static string BuildContainsPatternExpression(List<(object? Key, List<object?> Elements)> groups, FunctionOptimizerContext context)
	{
		if (groups.Count == 0)
		{
			return "false";
		}

		// Build: key is <lit1> or <lit2> or ...
		PatternSyntax pattern = SyntaxFactory.ConstantPattern(
			SyntaxFactory.ParseExpression(FormatLiteral(groups[0].Key)));

		for (var i = 1; i < groups.Count; i++)
		{
			pattern = SyntaxFactory.BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				SyntaxFactory.ConstantPattern(
					SyntaxFactory.ParseExpression(FormatLiteral(groups[i].Key))));
		}

		ExpressionSyntax isExpression = SyntaxFactory.IsPatternExpression(
			SyntaxFactory.IdentifierName("key"),
			pattern);

		// Let context.Visit optimise the expression (e.g. range checks)
		var visited = context.Visit(isExpression) ?? isExpression;

		return FormattingHelper.Render(visited);
	}

	/// <summary>
	/// Builds the source code string for a custom ILookup struct implementation.
	/// </summary>
	private static string BuildLookupStructSource(string structName, string keyTypeName, string elementTypeName, List<(object? Key, List<object?> Elements)> groups, string containsExpression)
	{
		var sb = new StringBuilder();

		sb.AppendLine($"file struct {structName} : ILookup<{keyTypeName}, {elementTypeName}>");
		sb.AppendLine("{");

		// Count property
		sb.AppendLine($"\tpublic int Count => {groups.Count};");
		sb.AppendLine();

		if (groups.Count == 0)
		{
			// Indexer
			sb.AppendLine($"\tpublic IEnumerable<{elementTypeName}> this[{keyTypeName} key] => [];");
		}
		else
		{
			// Indexer
			sb.AppendLine($"\tpublic IEnumerable<{elementTypeName}> this[{keyTypeName} key] => key switch");
			sb.AppendLine("\t{");

			foreach (var (key, elements) in groups)
			{
				var elementsStr = string.Join(", ", elements.Select(FormatLiteral));
				sb.AppendLine($"\t\t{FormatLiteral(key)} => [{elementsStr}],");
			}

			sb.AppendLine("\t\t_ => []");
			sb.AppendLine("\t};");
		}

		sb.AppendLine();

		// Contains method – body is the (possibly further-optimised) pattern expression
		sb.AppendLine($"\tpublic bool Contains({keyTypeName} key) => {containsExpression};");
		sb.AppendLine();

		// GetEnumerator
		sb.AppendLine($"\tpublic IEnumerator<IGrouping<{keyTypeName}, {elementTypeName}>> GetEnumerator()");
		sb.AppendLine("\t{");

		if (groups.Count == 0)
		{
			sb.AppendLine($"\t\treturn Enumerable.Empty<IGrouping<{keyTypeName}, {elementTypeName}>>().GetEnumerator();");
		}
		else
		{
			foreach (var (key, elements) in groups)
			{
				var elementsStr = string.Join(", ", elements.Select(FormatLiteral));
				sb.AppendLine($"\t\tyield return new Grouping<{keyTypeName}, {elementTypeName}>({FormatLiteral(key)}, {elementsStr});");
			}
		}

		sb.AppendLine("\t}");
		sb.AppendLine();

		// Explicit IEnumerable.GetEnumerator
		sb.AppendLine("\tIEnumerator IEnumerable.GetEnumerator() => GetEnumerator();");

		sb.AppendLine("}");

		return sb.ToString();
	}

	/// <summary>
	/// Formats a value as a C# literal string.
	/// </summary>
	private static string FormatLiteral(object? value)
	{
		return value switch
		{
			null => "null",
			string s => $"\"{s}\"",
			char c => $"'{c}'",
			bool b => b ? "true" : "false",
			float f => $"{f}F",
			double d => $"{d}D",
			decimal m => $"{m}M",
			long l => $"{l}L",
			ulong ul => $"{ul}UL",
			uint ui => $"{ui}U",
			_ => value.ToString()
		};
	}
}