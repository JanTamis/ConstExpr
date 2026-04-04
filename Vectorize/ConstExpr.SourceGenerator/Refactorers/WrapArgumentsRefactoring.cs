using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that wraps or unwraps long argument lists, parameter lists,
/// and chained method calls.
/// Inspired by the Roslyn <c>CSharpWrappingCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Wrap arguments: puts each argument on its own line.</item>
///   <item>Unwrap arguments: collapses multi-line arguments to a single line.</item>
///   <item>Wrap chained calls: puts each <c>.Method()</c> call on its own line.</item>
/// </list>
/// </summary>
public static class WrapArgumentsRefactoring
{
	/// <summary>
	/// Wraps an argument list so that each argument is on its own line.
	/// </summary>
	public static bool TryWrapArguments(
		ArgumentListSyntax argumentList,
		[NotNullWhen(true)] out ArgumentListSyntax? result)
	{
		result = null;

		if (argumentList.Arguments.Count < 2)
		{
			return false;
		}

		var newArguments = new List<SyntaxNodeOrToken>();

		for (var i = 0; i < argumentList.Arguments.Count; i++)
		{
			var arg = argumentList.Arguments[i].WithoutTrivia();

			if (i > 0)
			{
				arg = arg.WithLeadingTrivia(LineFeed, Tab);
			}

			newArguments.Add(arg);

			if (i < argumentList.Arguments.Count - 1)
			{
				newArguments.Add(Token(SyntaxKind.CommaToken));
			}
		}

		result = argumentList.WithArguments(SeparatedList<ArgumentSyntax>(newArguments));
		return true;
	}

	/// <summary>
	/// Unwraps an argument list so that all arguments are on a single line.
	/// </summary>
	public static bool TryUnwrapArguments(
		ArgumentListSyntax argumentList,
		[NotNullWhen(true)] out ArgumentListSyntax? result)
	{
		result = null;

		if (argumentList.Arguments.Count < 2)
		{
			return false;
		}

		var newArguments = new List<SyntaxNodeOrToken>();

		for (var i = 0; i < argumentList.Arguments.Count; i++)
		{
			var arg = argumentList.Arguments[i].WithoutTrivia();

			if (i > 0)
			{
				arg = arg.WithLeadingTrivia(Space);
			}

			newArguments.Add(arg);

			if (i < argumentList.Arguments.Count - 1)
			{
				newArguments.Add(Token(SyntaxKind.CommaToken));
			}
		}

		result = argumentList.WithArguments(SeparatedList<ArgumentSyntax>(newArguments));
		return true;
	}

	/// <summary>
	/// Wraps a parameter list so that each parameter is on its own line.
	/// </summary>
	public static bool TryWrapParameters(
		ParameterListSyntax parameterList,
		[NotNullWhen(true)] out ParameterListSyntax? result)
	{
		result = null;

		if (parameterList.Parameters.Count < 2)
		{
			return false;
		}

		var newParameters = new List<SyntaxNodeOrToken>();

		for (var i = 0; i < parameterList.Parameters.Count; i++)
		{
			var param = parameterList.Parameters[i].WithoutTrivia();

			if (i > 0)
			{
				param = param.WithLeadingTrivia(LineFeed, Tab);
			}

			newParameters.Add(param);

			if (i < parameterList.Parameters.Count - 1)
			{
				newParameters.Add(Token(SyntaxKind.CommaToken));
			}
		}

		result = parameterList.WithParameters(SeparatedList<ParameterSyntax>(newParameters));
		return true;
	}

	/// <summary>
	/// Wraps a chained method call so that each <c>.Method()</c> is on its own line.
	/// </summary>
	public static bool TryWrapChainedCalls(
		InvocationExpressionSyntax invocation,
		[NotNullWhen(true)] out InvocationExpressionSyntax? result)
	{
		result = null;

		// Collect the chain
		var chain = new List<InvocationExpressionSyntax>();
		ExpressionSyntax? current = invocation;

		while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } inv)
		{
			chain.Add(inv);
			current = ((MemberAccessExpressionSyntax)inv.Expression).Expression;
		}

		if (chain.Count < 2)
		{
			return false;
		}

		// Reverse to process from root
		chain.Reverse();

		// Build from the root outward, adding newlines before each dot
		var root = current!;

		foreach (var call in chain)
		{
			var memberAccess = (MemberAccessExpressionSyntax)call.Expression;
			var newMemberAccess = MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				root,
				memberAccess.Name)
				.WithOperatorToken(
					memberAccess.OperatorToken.WithLeadingTrivia(
						LineFeed, Tab));

			root = InvocationExpression(newMemberAccess, call.ArgumentList);
		}

		result = root as InvocationExpressionSyntax;
		return result is not null;
	}
}

