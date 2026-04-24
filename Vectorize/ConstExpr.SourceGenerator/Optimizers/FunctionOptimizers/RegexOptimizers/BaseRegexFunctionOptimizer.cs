using System;
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
/// Base class for optimizers that target <c>System.Text.RegularExpressions.Regex</c> methods.
/// Subclasses are discovered via reflection (same pattern as Math/Linq/Simd optimizers).
/// </summary>
public abstract class BaseRegexFunctionOptimizer(string name, Func<int, bool> isValidParameterCount) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public Func<int, bool> IsValidParameterCount { get; } = isValidParameterCount;

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsRegexMethod(context.Method))
		{
			result = null;
			return false;
		}

		return TryOptimizeRegex(context, out result);
	}

	protected abstract bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result);

	protected InvocationExpressionSyntax GetRegexInvocation(FunctionOptimizerContext context)
	{
		// Build a deterministic field name from the constant constructor arguments.
		var patternKey = String.Concat(
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

		return InvocationExpression(MemberAccessExpression(IdentifierName(variableName), IdentifierName(context.Method.Name)))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(context.VisitedParameters[0]))));
	}

	private bool IsRegexMethod(IMethodSymbol method)
	{
		return method.Name == Name
		       && method.IsStatic
		       && method.ContainingType.ToString() == "System.Text.RegularExpressions.Regex"
		       && IsValidParameterCount(method.Parameters.Length);
	}
}