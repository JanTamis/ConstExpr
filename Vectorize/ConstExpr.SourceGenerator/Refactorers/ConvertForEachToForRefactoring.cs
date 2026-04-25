using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between <c>foreach</c> loops and equivalent <c>for</c> loops
/// over indexable collections (arrays, lists with indexer and Count/Length).
/// Inspired by the Roslyn <c>ConvertForEachToForCodeRefactoringProvider</c>.
///
/// Since this is a pure syntax-level refactoring without a semantic model, it makes
/// best-effort assumptions:
/// <list type="bullet">
///   <item>Arrays use <c>.Length</c></item>
///   <item>Other collections use <c>.Count</c></item>
/// </list>
///
/// <code>
/// foreach (var item in collection) { Body(item); }
/// </code>
/// →
/// <code>
/// for (var i = 0; i &lt; collection.Length; i++) { var item = collection[i]; Body(item); }
/// </code>
/// </summary>
public static class ConvertForEachToForRefactoring
{
	/// <summary>
	/// Converts a foreach statement to a for loop with an index variable.
	/// </summary>
	/// <param name="forEach">The foreach statement to convert.</param>
	/// <param name="useLength">
	/// When <see langword="true"/>, uses <c>.Length</c>; otherwise uses <c>.Count</c>.
	/// Default is <see langword="true"/> (suitable for arrays).
	/// When a <paramref name="semanticModel"/> is provided, this parameter is auto-detected.
	/// </param>
	/// <param name="indexName">Name of the index variable. Defaults to <c>"i"</c>.</param>
	/// <param name="result">The resulting for statement.</param>
	/// <param name="semanticModel">
	/// Optional semantic model. When provided, the collection type is resolved to
	/// auto-detect whether to use <c>.Length</c> (arrays) or <c>.Count</c> (lists/collections).
	/// </param>
	public static bool TryConvertForEachToFor(
		ForEachStatementSyntax forEach,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out ForStatementSyntax? result,
		bool useLength = true,
		string indexName = "i")
	{
		result = null;

		var collection = forEach.Expression;
		var itemType = forEach.Type;
		var itemName = forEach.Identifier;

		// auto-detect Length vs Count from the collection type
		var collectionType = semanticModel.GetTypeInfo(collection).Type;

		if (collectionType is IArrayTypeSymbol)
		{
			useLength = true;
		}
		else if (collectionType is not null)
		{
			// Check if the type has a Count property (ICollection, IList, etc.)
			var hasCount = collectionType.GetMembers("Count")
				.OfType<IPropertySymbol>()
				.Any();
			var hasLength = collectionType.GetMembers("Length")
				.OfType<IPropertySymbol>()
				.Any();

			if (hasCount && !hasLength)
			{
				useLength = false;
			}
			else if (hasLength)
			{
				useLength = true;
			}
		}

		var lengthProperty = useLength ? "Length" : "Count";

		// var i = 0
		var indexDeclaration = VariableDeclaration(
			IdentifierName("var"),
			SingletonSeparatedList(
				VariableDeclarator(Identifier(indexName))
					.WithInitializer(EqualsValueClause(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))));

		// i < collection.Length
		var condition = LessThanExpression(
			IdentifierName(indexName),
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				collection,
				IdentifierName(lengthProperty)));

		// i++
		var incrementor = PostfixUnaryExpression(
			SyntaxKind.PostIncrementExpression,
			IdentifierName(indexName));

		// var item = collection[i];
		var itemDeclaration = LocalDeclarationStatement(
			VariableDeclaration(
				itemType,
				SingletonSeparatedList(
					VariableDeclarator(itemName)
						.WithInitializer(EqualsValueClause(
							ElementAccessExpression(
								collection,
								BracketedArgumentList(
									SingletonSeparatedList(
										Argument(IdentifierName(indexName))))))))));

		// Build the body: prepend item declaration to the existing body
		var body = forEach.Statement is BlockSyntax block
			? block.WithStatements(block.Statements.Insert(0, itemDeclaration))
			: Block(itemDeclaration, forEach.Statement);

		result = ForStatement(
				indexDeclaration,
				default,
				condition,
				SingletonSeparatedList<ExpressionSyntax>(incrementor),
				body)
			.WithTriviaFrom(forEach);

		return true;
	}

	/// <summary>
	/// Converts a simple for-loop (with index over Length/Count) back to a foreach loop.
	/// The for-loop must have the pattern:
	/// <c>for (var i = 0; i &lt; collection.Length; i++) { var item = collection[i]; ... }</c>
	/// </summary>
	public static bool TryConvertForToForEach(
		ForStatementSyntax forStatement,
		[NotNullWhen(true)] out ForEachStatementSyntax? result)
	{
		result = null;

		// Must have a declaration with a single variable initialized to 0
		if (forStatement.Declaration is not { Variables: [ var indexVar ] } 
		    || indexVar.Initializer?.Value is not LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NumericLiteralExpression } initLiteral 
		    || initLiteral.Token.Value is not 0)
		{
			return false;
		}

		// Condition: i < collection.Length (or .Count)
		if (forStatement.Condition is not BinaryExpressionSyntax
		    {
			    RawKind: (int) SyntaxKind.LessThanExpression,
			    Left: IdentifierNameSyntax condLeft,
			    Right: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" or "Count" } memberAccess
		    } 
		    || condLeft.Identifier.ValueText != indexVar.Identifier.ValueText)
		{
			return false;
		}

		var collection = memberAccess.Expression;

		// Incrementor: i++ or ++i or i += 1
		if (forStatement.Incrementors.Count != 1 ||
		    !IsSimpleIncrement(forStatement.Incrementors[0], indexVar.Identifier.ValueText))
		{
			return false;
		}

		// Body must start with: var item = collection[i];
		if (forStatement.Statement is not BlockSyntax block 
		    || block.Statements.Count < 1 
		    || block.Statements[0] is not LocalDeclarationStatementSyntax
		    {
			    Declaration.Variables: [ var itemVar ]
		    } localDecl)
		{
			return false;
		}

		if (itemVar.Initializer?.Value is not ElementAccessExpressionSyntax
		    {
			    ArgumentList.Arguments: [ { Expression: IdentifierNameSyntax argId } ]
		    } 
		    || argId.Identifier.ValueText != indexVar.Identifier.ValueText)
		{
			return false;
		}

		// Build the foreach
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
			PostfixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PostIncrementExpression, Operand: IdentifierNameSyntax id }
				=> id.Identifier.ValueText == variableName,
			PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PreIncrementExpression, Operand: IdentifierNameSyntax id }
				=> id.Identifier.ValueText == variableName,
			AssignmentExpressionSyntax
			{
				RawKind: (int) SyntaxKind.AddAssignmentExpression,
				Left: IdentifierNameSyntax id,
				Right: LiteralExpressionSyntax lit
			} => id.Identifier.ValueText == variableName && lit.Token.Value is 1,
			_ => false
		};
	}
}