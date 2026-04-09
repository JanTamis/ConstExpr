using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Collapses multi-range ASCII character classification <c>||</c> chains into the
/// corresponding <c>Char.IsXxx</c> API call.
/// <para>
/// The inner <c>&amp;&amp;</c> expressions are pre-optimized by
/// <see cref="ConditionalAndStrategies.ConditionalAndCharOptimizer"/> before this
/// strategy runs, so each range arrives as one of:
/// <c>Char.IsAsciiDigit(c)</c>, <c>Char.IsAsciiLetterLower(c)</c>,
/// <c>Char.IsAsciiLetterUpper(c)</c>, or <c>Char.IsBetween(c, low, high)</c>.
/// </para>
/// <list type="table">
///   <listheader><term>Source pattern</term><term>Result</term></listheader>
///   <item><term><c>(c>='0'&amp;&amp;c&lt;='9')||(c>='a'&amp;&amp;c&lt;='f')</c></term><term><c>Char.IsAsciiHexDigitLower(c)</c></term></item>
///   <item><term><c>(c>='0'&amp;&amp;c&lt;='9')||(c>='A'&amp;&amp;c&lt;='F')</c></term><term><c>Char.IsAsciiHexDigitUpper(c)</c></term></item>
///   <item><term><c>(c>='a'&amp;&amp;c&lt;='z')||(c>='A'&amp;&amp;c&lt;='Z')</c></term><term><c>Char.IsAsciiLetter(c)</c></term></item>
///   <item><term><c>(c>='0'&amp;&amp;c&lt;='9')||(c>='a'&amp;&amp;c&lt;='f')||(c>='A'&amp;&amp;c&lt;='F')</c></term><term><c>Char.IsAsciiHexDigit(c)</c></term></item>
///   <item><term><c>(c>='0'&amp;&amp;c&lt;='9')||(c>='a'&amp;&amp;c&lt;='z')||(c>='A'&amp;&amp;c&lt;='Z')</c></term><term><c>Char.IsAsciiLetterOrDigit(c)</c></term></item>
/// </list>
/// All orderings of the ranges are accepted.
/// </summary>
public class ConditionalOrAsciiCharRangeStrategy : BaseBinaryStrategy
{
	/// <summary>Known range-set → Char method name mappings.</summary>
	private static readonly (HashSet<(char Low, char High)> Ranges, string Method)[] KnownPatterns =
	[
		// single range patterns
		([ ('\x00', '\x7F') ], "IsAscii"),
		([ ('0', '9') ], "IsAsciiDigit"),
		([ ('a', 'z') ], "IsAsciiLetterLower"),
		([ ('A', 'Z') ], "IsAsciiLetterUpper"),
		// 2-range patterns
		([ ('0', '9'), ('a', 'f') ], "IsAsciiHexDigitLower"),
		([ ('0', '9'), ('A', 'F') ], "IsAsciiHexDigitUpper"),
		([ ('A', 'Z'), ('a', 'z') ], "IsAsciiLetter"),
		// 3-range patterns
		([ ('0', '9'), ('a', 'f'), ('A', 'F') ], "IsAsciiHexDigit"),
		([ ('0', '9'), ('A', 'Z'), ('a', 'z') ], "IsAsciiLetterOrDigit"),
	];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Flatten both sides of the || into individual leaf expressions.
		var parts = new List<ExpressionSyntax>();
		FlattenOrChain(context.Left.Syntax, parts);
		FlattenOrChain(context.Right.Syntax, parts);

		ExpressionSyntax? charExpr = null;
		var ranges = new List<(char Low, char High)>();

		foreach (var part in parts)
		{
			// TryExtractCharRanges may add multiple ranges for pre-optimized 2-range methods
			// (e.g. IsAsciiHexDigitLower contributes ('0','9') and ('a','f') at once).
			if (!TryExtractCharRanges(part, out var expr, ranges))
			{
				optimized = null;
				return false;
			}

			if (charExpr is null)
			{
				charExpr = expr;
			}
			else if (expr is null || !LeftEqualsRight(charExpr, expr, context.Variables))
			{
				// All parts must test the same char expression
				optimized = null;
				return false;
			}
		}

		var method = GetMethodName(ranges);

		if (charExpr is null || method is null)
		{
			optimized = null;
			return false;
		}

		if (context.TryGetValue(charExpr, out var charValue))
		{
			// get method via reflection and invoke it on the constant char value to fold the entire expression to a bool constant
			var charType = typeof(char);
			var charMethod = charType.GetMethod(method, [ charType ]);

			if (charMethod is null)
			{
				optimized = null;
				return false;
			}

			return TryCreateLiteral(charMethod.Invoke(null, [ charValue ]), out optimized);
		}

		// Emit: Char.IsXxx(c)
		optimized = InvocationExpression(
				MemberAccessExpression(
					IdentifierName("Char"),
					IdentifierName(method)))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(charExpr))));

		return true;
	}

	/// <summary>
	/// Recursively flattens a chain of <c>||</c> expressions into individual leaf nodes.
	/// </summary>
	private static void FlattenOrChain(ExpressionSyntax expr, List<ExpressionSyntax> parts)
	{
		switch (expr)
		{
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } binary:
			{
				FlattenOrChain(binary.Left, parts);
				FlattenOrChain(binary.Right, parts);
				break;
			}
			case ParenthesizedExpressionSyntax paren:
			{
				FlattenOrChain(paren.Expression, parts);
				break;
			}
			default:
			{
				parts.Add(expr);
				break;
			}
		}
	}

	/// <summary>
	/// Extracts the tested char expression and appends all char bounds to
	/// <paramref name="ranges"/> from a pre-optimized invocation.
	/// <list type="bullet">
	///   <item>Single-range: <c>Char.IsAscii</c>, <c>Char.IsAsciiDigit</c>, <c>Char.IsAsciiLetterLower</c>, <c>Char.IsAsciiLetterUpper</c></item>
	///   <item>Two-range: <c>Char.IsAsciiHexDigitLower</c>, <c>Char.IsAsciiHexDigitUpper</c>, <c>Char.IsAsciiLetter</c>
	///         — decomposed so the 3-range patterns can still be recognized.</item>
	///   <item>Three-range: <c>Char.IsAsciiHexDigit</c>, <c>Char.IsAsciiLetterOrDigit</c>
	///         — decomposed so they can participate in further compositions.</item>
	///   <item>Arbitrary: <c>Char.IsBetween(c, low, high)</c></item>
	/// </list>
	/// </summary>
	private static bool TryExtractCharRanges(ExpressionSyntax expr, out ExpressionSyntax? charExpr, List<(char Low, char High)> ranges)
	{
		charExpr = null;

		if (expr is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
		{
			return false;
		}

		var methodName = memberAccess.Name.Identifier.Text;

		if (invocation.ArgumentList.Arguments.Count == 1)
		{
			// Single-range named methods (produced by ConditionalAndCharOptimizer)
			(char L, char H)? single = methodName switch
			{
				"IsAscii" => ('\x00', '\x7F'),
				"IsAsciiDigit" => ('0', '9'),
				"IsAsciiLetterLower" => ('a', 'z'),
				"IsAsciiLetterUpper" => ('A', 'Z'),
				_ => null
			};

			if (single is not null)
			{
				charExpr = invocation.ArgumentList.Arguments[0].Expression;
				ranges.Add(single.Value);
				return true;
			}

			// Two-range methods that may have been produced by a previous run of this
			// strategy — decompose them so they can compose into larger patterns.
			(char L1, char H1, char L2, char H2)? dual = methodName switch
			{
				"IsAsciiHexDigitLower" => ('0', '9', 'a', 'f'),
				"IsAsciiHexDigitUpper" => ('0', '9', 'A', 'F'),
				"IsAsciiLetter" => ('A', 'Z', 'a', 'z'),
				_ => null
			};

			if (dual is not null)
			{
				charExpr = invocation.ArgumentList.Arguments[0].Expression;
				ranges.Add((dual.Value.L1, dual.Value.H1));
				ranges.Add((dual.Value.L2, dual.Value.H2));
				return true;
			}

			// Three-range methods that may have been produced by a previous run of this
			// strategy — decompose them so they can compose into larger patterns.
			(char L1, char H1, char L2, char H2, char L3, char H3)? triple = methodName switch
			{
				"IsAsciiHexDigit" => ('0', '9', 'A', 'F', 'a', 'f'),
				"IsAsciiLetterOrDigit" => ('0', '9', 'A', 'Z', 'a', 'z'),
				_ => null
			};

			if (triple is not null)
			{
				charExpr = invocation.ArgumentList.Arguments[0].Expression;
				ranges.Add((triple.Value.L1, triple.Value.H1));
				ranges.Add((triple.Value.L2, triple.Value.H2));
				ranges.Add((triple.Value.L3, triple.Value.H3));
				return true;
			}
		}

		// IsBetween(c, low, high)  — used for ranges without a dedicated named method
		if (methodName == "IsBetween"
		    && invocation.ArgumentList.Arguments is
			    [ _, { Expression: LiteralExpressionSyntax { Token.Value: char lowChar } }, { Expression: LiteralExpressionSyntax { Token.Value: char highChar } } ])
		{
			charExpr = invocation.ArgumentList.Arguments[0].Expression;
			ranges.Add((lowChar, highChar));
			return true;
		}

		return false;
	}

	/// <summary>
	/// Returns the <c>Char.IsXxx</c> method name whose range-set exactly matches
	/// <paramref name="ranges"/>, or <see langword="null"/> if no known pattern matches.
	/// </summary>
	private static string? GetMethodName(List<(char Low, char High)> ranges)
	{
		foreach (var (knownRanges, method) in KnownPatterns)
		{
			if (ranges.Count == knownRanges.Count
			    && ranges.All(r => knownRanges.Contains(r)))
			{
				return method;
			}
		}

		return null;
	}
}