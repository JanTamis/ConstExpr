using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts a <c>for</c> loop (with an index over <c>Length</c>/<c>Count</c>)
/// back to a <c>foreach</c> loop.
/// Inspired by the Roslyn <c>CSharpConvertForToForEachCodeRefactoringProvider</c>.
///
/// <code>
/// for (var i = 0; i &lt; collection.Length; i++)
/// {
///     var item = collection[i];
///     Body(item);
/// }
/// </code>
/// →
/// <code>
/// foreach (var item in collection)
/// {
///     Body(item);
/// }
/// </code>
///
/// The for-loop must have the pattern:
/// <c>for (var i = 0; i &lt; collection.Length/Count; i++) { var item = collection[i]; ... }</c>
/// </summary>
public static class ConvertForToForEachRefactoring
{
	/// <summary>
	/// Converts a simple for-loop (with index over Length/Count) to a foreach loop.
	/// </summary>
	public static bool TryConvertForToForEach(
		ForStatementSyntax forStatement,
		[NotNullWhen(true)] out ForEachStatementSyntax? result)
	{
		result = null;

		// Must have a declaration with a single variable initialized to 0
		if (forStatement.Declaration is not { Variables: [ var indexVar ] }
		    || indexVar.Initializer?.Value is not LiteralExpressionSyntax initLiteral
		    || !initLiteral.IsKind(SyntaxKind.NumericLiteralExpression)
		    || initLiteral.Token.Value is not 0)
		{
			return false;
		}

		var indexName = indexVar.Identifier.ValueText;

		// Condition: i < collection.Length (or .Count)
		if (forStatement.Condition is not BinaryExpressionSyntax condition
		    || !condition.IsKind(SyntaxKind.LessThanExpression))
		{
			return false;
		}

		if (condition.Left is not IdentifierNameSyntax condLeft
		    || condLeft.Identifier.ValueText != indexName)
		{
			return false;
		}

		if (condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" or "Count" } memberAccess)
		{
			return false;
		}

		var collection = memberAccess.Expression;

		// Incrementor: i++ or ++i or i += 1
		if (forStatement.Incrementors.Count != 1
		    || !IsSimpleIncrement(forStatement.Incrementors[0], indexName))
		{
			return false;
		}

		// Body must start with: var/Type item = collection[i];
		if (forStatement.Statement is not BlockSyntax block
		    || block.Statements.Count < 1)
		{
			return false;
		}

		if (block.Statements[0] is not LocalDeclarationStatementSyntax { Declaration.Variables: [ var itemVar ] } localDecl)
		{
			return false;
		}

		if (itemVar.Initializer?.Value is not ElementAccessExpressionSyntax elementAccess)
		{
			return false;
		}

		if (elementAccess.ArgumentList.Arguments.Count != 1
		    || elementAccess.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax argId
		    || argId.Identifier.ValueText != indexName)
		{
			return false;
		}

		// Build the foreach body (remaining statements after the item decl)
		var remainingStatements = block.Statements.RemoveAt(0);

		var body = remainingStatements.Count == 1
			? remainingStatements[0]
			: Block(remainingStatements);

		result = ForEachStatement(
				localDecl.Declaration.Type,
				itemVar.Identifier,
				collection,
				body)
			.WithTriviaFrom(forStatement);

		return true;
	}

	private static bool IsSimpleIncrement(ExpressionSyntax expr, string variableName)
	{
		return expr switch
		{
			PostfixUnaryExpressionSyntax post when 
				post.IsKind(SyntaxKind.PostIncrementExpression) && post.Operand is IdentifierNameSyntax postId && postId.Identifier.ValueText == variableName => true,
			PrefixUnaryExpressionSyntax pre when 
				pre.IsKind(SyntaxKind.PreIncrementExpression) && pre.Operand is IdentifierNameSyntax preId && preId.Identifier.ValueText == variableName => true,
			AssignmentExpressionSyntax assign when 
				assign.IsKind(SyntaxKind.AddAssignmentExpression) && assign.Left is IdentifierNameSyntax assignId && assignId.Identifier.ValueText == variableName && assign.Right is LiteralExpressionSyntax { Token.Value: 1 } => true,
			_ => false
		};
	}
}