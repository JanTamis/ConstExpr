using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector)</c> as an intermediate step.
/// Builds a lookup from the inner collection, then for each outer element passes the matching
/// inner group to the result selector. Results are buffered and processed after the main loop.
/// </summary>
public class GroupJoinLinqUnroller : BaseLinqUnroller
{
	private const string LookupName = "groupJoinLookup";
	private const string BufferName = "groupJoinBuffer";
	private const string EmptyListName = "groupJoinEmpty";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// TypeArguments: [TOuter, TInner, TKey, TResult]
		var keyType = method.MethodSymbol.TypeArguments[2];
		var innerType = method.MethodSymbol.TypeArguments[1];
		var resultType = method.MethodSymbol.TypeArguments[3];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var innerTypeName = method.Model.Compilation.GetMinimalString(innerType);
		var resultTypeName = method.Model.Compilation.GetMinimalString(resultType);

		// var groupJoinLookup = new Dictionary<TKey, List<TInner>>();
		statements.Add(CreateLocalDeclaration(LookupName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, List<{innerTypeName}>>"), [])));

		// Build the lookup from inner collection
		if (method.Parameters.Length >= 2
		    && TryGetLambda(method.Parameters[1], out var innerKeyLambda))
		{
			var innerKeyExpr = ReplaceLambda(method.Visit(innerKeyLambda) as LambdaExpressionSyntax ?? innerKeyLambda, IdentifierName("innerItem"));

			if (innerKeyExpr is not null)
			{
				statements.Add(ForEachStatement(
					IdentifierName("var"),
					"innerItem",
					method.Parameters[0],
					Block(
						CreateLocalDeclaration("innerKey", innerKeyExpr),
						IfStatement(
							LogicalNotExpression(InvocationExpression(
									MemberAccessExpression(IdentifierName(LookupName), IdentifierName("TryGetValue")))
								.WithArgumentList(ArgumentList(SeparatedList([
									Argument(IdentifierName("innerKey")),
									Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("innerList"))))
										.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
								])))),
							Block(
								CreateAssignment("innerList", ObjectCreationExpression(IdentifierName($"List<{innerTypeName}>"), [])),
								ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
									ElementAccessExpression(IdentifierName(LookupName), IdentifierName("innerKey")),
									IdentifierName("innerList"))))),
						ExpressionStatement(CreateMethodInvocation(IdentifierName("innerList"), "Add", IdentifierName("innerItem"))))));
			}
		}

		// var groupJoinEmpty = new List<TInner>();  (shared empty list for unmatched keys)
		statements.Add(CreateLocalDeclaration(EmptyListName,
			ObjectCreationExpression(IdentifierName($"List<{innerTypeName}>"), [])));

		// var groupJoinBuffer = new List<TResult>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{resultTypeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 4
		    || !TryGetLambda(method.Parameters[1], out _)
		    || !TryGetLambda(method.Parameters[3], out var resultLambda))
		{
			return;
		}

		// outerKeySelector is Parameters[1] for Join but for the actual LINQ extension method
		// the parameters are (inner, outerKeySelector, innerKeySelector, resultSelector)
		// After parsing: Parameters[0]=inner, Parameters[1]=outerKeySelector, Parameters[2]=innerKeySelector, Parameters[3]=resultSelector
		if (!TryGetLambda(method.Parameters[1], out var outerKeyLambda))
		{
			return;
		}

		var outerKeyExpr = ReplaceLambda(method.Visit(outerKeyLambda) as LambdaExpressionSyntax ?? outerKeyLambda, elementName);

		if (outerKeyExpr is null)
		{
			return;
		}

		// var outerKey = outerKeySelector(item);
		statements.Add(CreateLocalDeclaration("outerKey", outerKeyExpr));

		// if (!groupJoinLookup.TryGetValue(outerKey, out var matchedGroup)) matchedGroup = groupJoinEmpty;
		statements.Add(IfStatement(
			LogicalNotExpression(InvocationExpression(
					MemberAccessExpression(IdentifierName(LookupName), IdentifierName("TryGetValue")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(IdentifierName("outerKey")),
					Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("matchedGroup"))))
						.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
				])))),
			CreateAssignment("matchedGroup", IdentifierName(EmptyListName))));

		// groupJoinBuffer.Add(resultSelector(item, matchedGroup));
		var resultBody = ReplaceLambda(method.Visit(resultLambda) as LambdaExpressionSyntax ?? resultLambda, elementName);

		if (resultBody is not null
		    && resultLambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
		{
			var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
			var identifiers = resultBody
				.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Where(n => n.Identifier.Text == secondParam)
				.ToList();

			resultBody = resultBody.ReplaceNodes(identifiers, (_, _) => IdentifierName("matchedGroup"));
		}

		if (resultBody is not null)
		{
			statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", resultBody)));
		}

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in groupJoinBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

