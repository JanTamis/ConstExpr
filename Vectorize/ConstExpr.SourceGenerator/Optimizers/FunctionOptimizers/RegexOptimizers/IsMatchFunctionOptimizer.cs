using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes <c>Regex.IsMatch(input, pattern)</c> and <c>Regex.IsMatch(input, pattern, options)</c>.
///   <para>
///     When the constant pattern is really just a literal string - optionally anchored with <c>^</c> -
///     the call is lowered to the equivalent ordinal <c>string.StartsWith</c>/<c>string.Contains</c>,
///     which drops the regex engine <em>and</em> the hoisted <c>Regex</c> field entirely. The emitted
///     call then feeds the existing StartsWith/Contains string optimizers for a further round.
///   </para>
///   <para>
///     Otherwise it falls back to the shared behaviour: cache a compiled <see cref="Regex" /> instance
///     as a private static readonly field and call the instance method on it.
///   </para>
/// </summary>
public class IsMatchFunctionOptimizer() : BaseRegexFunctionOptimizer("IsMatch", n => n is 2 or 3 or 4)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryLowerToStringOperation(context, out result))
		{
			return true;
		}

		return base.TryOptimizeRegex(context, out result);
	}

	private bool TryLowerToStringOperation(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (!TryGetLowerablePattern(context, 1, 2, out var pattern)
		    || !RegexPatternClassifier.TryGetLiteral(pattern, out var literal, out var anchoredAtStart))
		{
			return false;
		}

		var input = context.VisitedParameters[0];

		// Regex.IsMatch(null, pattern) throws ArgumentNullException where input.StartsWith(..) would
		// throw NullReferenceException. Both throw, but the type differs, so only lower once the input is
		// provably non-null - the same UseNullableAnnotations-driven gate the string optimizers use.
		if (CanBeNull(context, input))
		{
			return false;
		}

		// StartsWith(string, StringComparison) has always existed, but Contains(string, StringComparison)
		// is .NET Core 2.1 / netstandard2.1+ - absent on .NET Framework and netstandard2.0. Fall back to
		// the cached field when the target compilation doesn't have it, the same way the Regex.Count
		// rewrite gates on its own availability.
		if (!anchoredAtStart
		    && !context.Model.Compilation.GetSpecialType(SpecialType.System_String).HasMethod(nameof(String.Contains), m => m.Parameters.Length == 2))
		{
			return false;
		}

		context.Usings.Add("System");

		result = InvocationExpression(
				MemberAccessExpression(ParenthesizeIfNeeded(input), IdentifierName(anchoredAtStart ? nameof(String.StartsWith) : nameof(String.Contains))))
			.WithArgumentList(ArgumentList(SeparatedList([
				Argument(CreateLiteral(literal)),
				Argument(MemberAccessExpression(IdentifierName(nameof(StringComparison)), IdentifierName(nameof(StringComparison.Ordinal))))
			])));

		return true;
	}

	/// <summary>
	///   Wraps the input in parentheses when moving it into member-access-receiver position could
	///   otherwise change what the surrounding expression means. Already-atomic expressions pass through.
	/// </summary>
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
	{
		return expression is IdentifierNameSyntax or LiteralExpressionSyntax or InvocationExpressionSyntax
			or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax or ObjectCreationExpressionSyntax
			or ParenthesizedExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax
			? expression
			: ParenthesizedExpression(expression);
	}
}