using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that adds or removes the <c>static</c> modifier on local functions.
/// Inspired by the Roslyn <c>MakeLocalFunctionStaticCodeRefactoringProvider</c>.
///
/// This is a pure syntax transformation — it only toggles the modifier without
/// analyzing captured variables. Semantic validation is left to the compiler.
/// </summary>
public static class MakeLocalFunctionStaticRefactoring
{
	/// <summary>
	/// Adds the <c>static</c> modifier to a local function that does not already have it.
	/// When a <paramref name="semanticModel"/> is provided, verifies that the local function
	/// does not capture any variables from the enclosing scope.
	/// </summary>
	public static bool TryMakeStatic(
		LocalFunctionStatementSyntax localFunction,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out LocalFunctionStatementSyntax? result)
	{
		result = null;

		if (localFunction.Modifiers.Any(SyntaxKind.StaticKeyword))
		{
			return false;
		}

		// Verify no variables are captured
		var bodyNode = (SyntaxNode?) localFunction.Body ?? localFunction.ExpressionBody;

		if (bodyNode is not null)
		{
			var dataFlow = semanticModel.AnalyzeDataFlow(bodyNode);

			if (dataFlow is { Succeeded: true, Captured.Length: > 0 })
			{
				return false;
			}
		}

		result = localFunction.AddModifiers(Token(SyntaxKind.StaticKeyword));
		return true;
	}

	/// <summary>
	/// Removes the <c>static</c> modifier from a local function.
	/// </summary>
	public static bool TryMakeNonStatic(
		LocalFunctionStatementSyntax localFunction,
		[NotNullWhen(true)] out LocalFunctionStatementSyntax? result)
	{
		result = null;

		var index = -1;

		for (var i = 0; i < localFunction.Modifiers.Count; i++)
		{
			if (localFunction.Modifiers[i].IsKind(SyntaxKind.StaticKeyword))
			{
				index = i;
				break;
			}
		}

		if (index < 0)
		{
			return false;
		}

		result = localFunction.WithModifiers(localFunction.Modifiers.RemoveAt(index));
		return true;
	}
}