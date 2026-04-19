using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Optimizes static <c>Regex.Replace</c> overloads by caching a compiled <see cref="Regex"/>
/// instance as a private static readonly field when the <c>pattern</c> (and optional
/// <c>options</c>) argument is a compile-time constant.
/// <list type="bullet">
///   <item><c>Regex.Replace(input, pattern, replacement)</c></item>
///   <item><c>Regex.Replace(input, pattern, replacement, options)</c></item>
///   <item><c>Regex.Replace(input, pattern, evaluator)</c></item>
///   <item><c>Regex.Replace(input, pattern, evaluator, options)</c></item>
/// </list>
/// The <c>input</c> and <c>replacement</c>/<c>evaluator</c> arguments may be runtime values.
/// </summary>
public class ReplaceFunctionOptimizer() : BaseRegexFunctionOptimizer("Replace", 3, 4)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// Pattern (param[1]) must be a compile-time constant.
		if (!TryGetLiteralValue(context.VisitedParameters[1], context, out _))
		{
			return false;
		}

		// For 4-argument overloads the options (param[3]) must also be constant.
		if (context.VisitedParameters.Count == 4 && TryGetLiteralValue(context.VisitedParameters[3], context, out _))
		{
			return false;
		}

		// Collect the constructor arguments for the cached Regex: pattern + optional options.
		var ctorArgs = context.VisitedParameters.Count == 4
			? new List<ExpressionSyntax> { context.VisitedParameters[1], context.VisitedParameters[3] }
			: new List<ExpressionSyntax> { context.VisitedParameters[1] };

		// Build a deterministic field name from the constant constructor arguments.
		var patternKey = string.Concat(
			ctorArgs.Select(s => TryGetLiteralValue(s, context, out var lit) && lit is string str ? str : s.ToFullString())
		);
		var variableName = $"Regex_{patternKey.GetDeterministicHashString()}";

		var field = FieldDeclaration(VariableDeclaration(IdentifierName(nameof(Regex)))
				.WithVariables(
					SingletonSeparatedList(
						VariableDeclarator(Identifier(variableName))
							.WithInitializer(EqualsValueClause(
								ObjectCreationExpression(IdentifierName(nameof(Regex)))
									.WithArgumentList(ArgumentList(SeparatedList(ctorArgs.Select(Argument)))))
							))
				))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));

		context.AdditionalSyntax.Add(field, true);
		context.Usings.Add("System.Text.RegularExpressions");

		// Instance Replace takes (input, replacement) — drop the pattern and options arguments.
		result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
					IdentifierName(variableName),
					IdentifierName(context.Method.Name)))
			.WithArgumentList(ArgumentList(SeparatedList([
				Argument(context.VisitedParameters[0]), // input
				Argument(context.VisitedParameters[2])  // replacement or MatchEvaluator
			])));

		return true;
	}
}

