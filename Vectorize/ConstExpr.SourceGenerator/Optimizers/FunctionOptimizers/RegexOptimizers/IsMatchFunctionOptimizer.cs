using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Optimizes <c>Regex.IsMatch(input, pattern)</c> and <c>Regex.IsMatch(input, pattern, options)</c>
/// by parsing the constant pattern at compile time and emitting an equivalent inline C# method
/// that operates on <c>ReadOnlySpan&lt;char&gt;</c>.
/// </summary>
public class IsMatchFunctionOptimizer() : BaseRegexFunctionOptimizer("IsMatch", n => n is 2 or 3)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// We only handle the static overloads:
		//   Regex.IsMatch(string input, string pattern)
		//   Regex.IsMatch(string input, string pattern, RegexOptions options)
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

		// Use the literal string values to build a stable deterministic hash.
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
		
		result = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(variableName), IdentifierName(context.Method.Name)))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(context.VisitedParameters[0]))));

		return true;
	}
}