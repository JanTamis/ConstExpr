using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.CountBy(keySelector)</c> as an intermediate step.
/// Builds a <c>Dictionary&lt;TKey, int&gt;</c> during the main loop counting
/// occurrences per key, then iterates the dictionary through subsequent chain steps.
/// </summary>
public class CountByLinqUnroller : BaseLinqUnroller
{
	private const string DictName = "countByDict";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var keyType = method.MethodSymbol.TypeArguments[^1];
		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);

		// var countByDict = new Dictionary<TKey, int>();
		statements.Add(CreateLocalDeclaration(DictName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, int>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1
		    || !TryGetLambda(method.Parameters[0], out var keyLambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(keyLambda) as LambdaExpressionSyntax ?? keyLambda, elementName);

		if (keyExpr is null)
		{
			return;
		}

		// var countKey = keySelector(item);
		statements.Add(CreateLocalDeclaration("countKey", keyExpr));

		// if (!countByDict.TryGetValue(countKey, out var countVal)) countVal = 0;
		statements.Add(IfStatement(
			LogicalNotExpression(InvocationExpression(
					MemberAccessExpression(IdentifierName(DictName), IdentifierName("TryGetValue")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(IdentifierName("countKey")),
					Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("countVal"))))
						.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
				])))),
			CreateAssignment("countVal", CreateLiteral(0)!)));

		// countByDict[countKey] = countVal + 1;
		statements.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
			ElementAccessExpression(IdentifierName(DictName), IdentifierName("countKey")),
			AddExpression(IdentifierName("countVal"), CreateLiteral(1)!))));

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in countByDict) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(DictName),
			Block(partialLoopBody)));
	}
}

