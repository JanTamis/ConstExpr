using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.LeftJoin(inner, outerKeySelector, innerKeySelector, resultSelector)</c> (.NET 10+)
/// as an intermediate step. Like Join but includes unmatched outer elements with a default inner value.
/// </summary>
public class LeftJoinLinqUnroller : BaseLinqUnroller
{
	private const string LookupName = "leftJoinLookup";
	private const string BufferName = "leftJoinBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// TypeArguments: [TOuter, TInner, TKey, TResult]
		var keyType = method.MethodSymbol.TypeArguments[2];
		var innerType = method.MethodSymbol.TypeArguments[1];
		var resultType = method.MethodSymbol.TypeArguments[3];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var innerTypeName = method.Model.Compilation.GetMinimalString(innerType);
		var resultTypeName = method.Model.Compilation.GetMinimalString(resultType);

		// var leftJoinLookup = new Dictionary<TKey, List<TInner>>();
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

		// var leftJoinBuffer = new List<TResult>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{resultTypeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 4
		    || !TryGetLambda(method.Parameters[1], out var outerKeyLambda)
		    || !TryGetLambda(method.Parameters[3], out var resultLambda))
		{
			return;
		}

		var outerKeyExpr = ReplaceLambda(method.Visit(outerKeyLambda) as LambdaExpressionSyntax ?? outerKeyLambda, elementName);

		if (outerKeyExpr is null)
		{
			return;
		}

		var innerType = method.MethodSymbol.TypeArguments[1];

		// var outerKey = outerKeySelector(item);
		statements.Add(CreateLocalDeclaration("outerKey", outerKeyExpr));

		// Capture ref parameter for use in local function
		var capturedElement = elementName;

		// Build result expression replacing both params
		ExpressionSyntax? MakeResult(ExpressionSyntax innerExpr)
		{
			var body = ReplaceLambda(method.Visit(resultLambda) as LambdaExpressionSyntax ?? resultLambda, capturedElement);

			if (body is not null
			    && resultLambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
			{
				var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
				var ids = body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
					.Where(n => n.Identifier.Text == secondParam).ToList();
				body = body.ReplaceNodes(ids, (_, _) => innerExpr);
			}

			return body;
		}

		// if (leftJoinLookup.TryGetValue(outerKey, out var matchedInners))
		// {
		//     foreach (var matched in matchedInners) leftJoinBuffer.Add(resultSelector(item, matched));
		// }
		// else
		// {
		//     leftJoinBuffer.Add(resultSelector(item, default));
		// }
		var matchedResult = MakeResult(IdentifierName("matched"));
		var defaultResult = MakeResult(innerType.GetDefaultValue());

		if (matchedResult is not null && defaultResult is not null)
		{
			statements.Add(IfStatement(
				InvocationExpression(
						MemberAccessExpression(IdentifierName(LookupName), IdentifierName("TryGetValue")))
					.WithArgumentList(ArgumentList(SeparatedList([
						Argument(IdentifierName("outerKey")),
						Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("matchedInners"))))
							.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
					]))),
				Block(ForEachStatement(
					IdentifierName("var"),
					"matched",
					IdentifierName("matchedInners"),
					Block(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", matchedResult))))),
				ElseClause(Block(
					ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", defaultResult))))));
		}

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}



