using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Vectorize.ConstExpr.SourceGenerator.BuildIn;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using RegexOptions = Vectorize.ConstExpr.SourceGenerator.BuildIn.RegexOptions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Optimizes <c>Regex.IsMatch(input, pattern)</c> and <c>Regex.IsMatch(input, pattern, options)</c>
/// by parsing the constant pattern at compile time and emitting an equivalent inline C# method
/// that operates on <c>ReadOnlySpan&lt;char&gt;</c>.
/// </summary>
public class IsMatchFunctionOptimizer() : BaseRegexFunctionOptimizer("IsMatch", 2, 3)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// We only handle the static overloads:
		//   Regex.IsMatch(string input, string pattern)
		//   Regex.IsMatch(string input, string pattern, RegexOptions options)
		if (!context.Method.IsStatic)
		{
			return false;
		}

		// ── extract the constant pattern ──
		if (context.VisitedParameters.Count < 2
		    || context.VisitedParameters[1] is not LiteralExpressionSyntax { Token.Value: string pattern })
		{
			return false;
		}

		// ── extract options (if present) ──
		var options = RegexOptions.None;

		if (context.VisitedParameters.Count >= 3
		    && !TryResolveRegexOptions(context, context.VisitedParameters[2], out options))
		{
			// Options are not a compile-time constant — bail out
			return false;
		}

		// ── safety: reject patterns / options we cannot yet handle ──
		if ((options & RegexOptions.RightToLeft) != 0 ||
		    (options & RegexOptions.NonBacktracking) != 0)
		{
			return false;
		}

		// Validate the pattern can be parsed
		RegexTree tree;

		try
		{
			tree = RegexParser.Parse(pattern, options, CultureInfo.InvariantCulture);
		}
		catch
		{
			return false;
		}

		if (!tree.Root.SupportsCompilation(out _))
		{
			return false;
		}

		// ── emit the method ──
		var emitter = new RegexCodeEmitter(context);
		MethodDeclarationSyntax method;

		try
		{
			method = emitter.EmitIsMatchMethod(pattern, options);

			method = method.WithBody(context.VisitStatement(method.Body) as BlockSyntax ?? method.Body);
		}
		catch
		{
			return false;
		}

		// Register the method so it gets included in the generated output
		context.AdditionalMethods.TryAdd(method, false);
		context.Usings.Add("System");

		// ── build the replacement call ──
		// The emitted method takes ReadOnlySpan<char>, so wrap the input string:
		//   IsMatch_XXXXXXXX(input.AsSpan())
		var inputArg = context.VisitedParameters[0];

		var asSpanCall = InvocationExpression(
			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				inputArg,
				IdentifierName("AsSpan")));

		result = InvocationExpression(IdentifierName(method.Identifier))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(asSpanCall))));

		return true;
	}

	/// <summary>
	/// Tries to resolve a <c>System.Text.RegularExpressions.RegexOptions</c> value from a syntax node.
	/// Supports integer literals and simple member access (e.g. <c>RegexOptions.IgnoreCase | RegexOptions.Multiline</c>).
	/// </summary>
	private static bool TryResolveRegexOptions(FunctionOptimizerContext context, ExpressionSyntax expr, out RegexOptions options)
	{
		options = RegexOptions.None;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: int intVal }:
				options = (RegexOptions)intVal;
				return true;

			case MemberAccessExpressionSyntax memberAccess:
				if (Enum.TryParse<RegexOptions>(memberAccess.Name.Identifier.Text, out var parsed))
				{
					options = parsed;
					return true;
				}
				return false;

			case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.BitwiseOrExpression):
				if (TryResolveRegexOptions(context, binary.Left, out var left) &&
				    TryResolveRegexOptions(context, binary.Right, out var right))
				{
					options = left | right;
					return true;
				}
				return false;

			case CastExpressionSyntax cast:
				return TryResolveRegexOptions(context, cast.Expression, out options);

			case ParenthesizedExpressionSyntax paren:
				return TryResolveRegexOptions(context, paren.Expression, out options);

			default:
				return false;
		}
	}
}

