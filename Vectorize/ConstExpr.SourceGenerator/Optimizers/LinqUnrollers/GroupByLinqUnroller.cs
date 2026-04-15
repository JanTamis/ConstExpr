using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.GroupBy(keySelector)</c> or <c>.GroupBy(keySelector, elementSelector)</c>
/// as an intermediate step. Collects all elements into a dictionary of lists keyed by
/// the key selector, then iterates the groups through subsequent chain steps.
/// </summary>
public class GroupByLinqUnroller : BaseLinqUnroller
{
	private const string DictName = "groupByDict";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var keyType = method.MethodSymbol.TypeArguments.Length >= 2
			? method.MethodSymbol.TypeArguments[1]
			: method.MethodSymbol.TypeArguments[0];

		// For GroupBy<TSource, TKey>, TypeArguments = [TSource, TKey]
		// We need Dictionary<TKey, List<TSource>> (or List<TElement> if element selector present)
		var sourceType = method.MethodSymbol.TypeArguments[0];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var sourceTypeName = method.Model.Compilation.GetMinimalString(sourceType);

		// var groupByDict = new Dictionary<TKey, List<TSource>>();
		statements.Add(CreateLocalDeclaration(DictName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, List<{sourceTypeName}>>"), [])));
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

		// var key = keySelector(item);
		statements.Add(CreateLocalDeclaration("groupKey", keyExpr));

		// if (!groupByDict.TryGetValue(key, out var list)) { list = new List<T>(); groupByDict[key] = list; }
		statements.Add(IfStatement(
			LogicalNotExpression(InvocationExpression(
					MemberAccessExpression(IdentifierName(DictName), IdentifierName("TryGetValue")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(IdentifierName("groupKey")),
					Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("groupList"))))
						.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
				])))),
			Block(
				CreateAssignment("groupList", ObjectCreationExpression(IdentifierName($"List<{method.Model.Compilation.GetMinimalString(method.MethodSymbol.TypeArguments[0])}>"), [])),
				ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
					ElementAccessExpression(IdentifierName(DictName), IdentifierName("groupKey")),
					IdentifierName("groupList"))))));

		// Get element to add (use element selector if present)
		ExpressionSyntax elementToAdd = elementName;
		if (method.Parameters.Length >= 2 && TryGetLambda(method.Parameters[1], out var elementLambda))
		{
			var selectedElement = ReplaceLambda(method.Visit(elementLambda) as LambdaExpressionSyntax ?? elementLambda, elementName);
			if (selectedElement is not null)
			{
				elementToAdd = selectedElement;
			}
		}

		// groupList.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName("groupList"), "Add", elementToAdd)));

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in groupByDict) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(DictName),
			Block(partialLoopBody)));
	}
}


