using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Optimizes <c>Regex.Matches(input, pattern)</c> and <c>Regex.Matches(input, pattern, options)</c>
/// by caching a compiled <see cref="Regex"/> instance as a private static readonly field and
/// replacing the static call with the equivalent instance method call.
/// </summary>
public class MatchesFunctionOptimizer() : BaseRegexFunctionOptimizer("Matches", 2, 3)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// Pattern (and optional options) must be constant; only input may be a runtime value.
		if (!TryGetLiteralValue(context.VisitedParameters[1], context, out _))
		{
			return false;
		}

		var literalParameterCount = context.VisitedParameters
			.Skip(1)
			.Count(x => TryGetLiteralValue(x, context, out _));

		if (literalParameterCount != context.VisitedParameters.Count - 1)
		{
			return false;
		}

		// Build a deterministic field name from the constant constructor arguments.
		var patternKey = string.Concat(
			context.VisitedParameters
				.Skip(1)
				.Select(s => TryGetLiteralValue(s, context, out var lit) && lit is string str ? str : s.ToFullString())
		);
		var variableName = $"Regex_{patternKey.GetDeterministicHashString()}";

		var field = FieldDeclaration(VariableDeclaration(IdentifierName(nameof(Regex)))
				.WithVariables(
					SingletonSeparatedList(
						VariableDeclarator(Identifier(variableName))
							.WithInitializer(EqualsValueClause(
								ObjectCreationExpression(IdentifierName(nameof(Regex)))
									.WithArgumentList(ArgumentList(SeparatedList(context.VisitedParameters.Skip(1).Select(Argument)))))
							))
				))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));

		context.AdditionalSyntax.Add(field, true);
		context.Usings.Add("System.Text.RegularExpressions");

		result = InvocationExpression(MemberAccessExpression(IdentifierName(variableName), IdentifierName(context.Method.Name)))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(context.VisitedParameters[0]))));

		return true;
	}
}

