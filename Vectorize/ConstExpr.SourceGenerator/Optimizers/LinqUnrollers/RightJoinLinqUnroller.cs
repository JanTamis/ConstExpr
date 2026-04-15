using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ConstExpr.SourceGenerator.Extensions;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.RightJoin(inner, outerKeySelector, innerKeySelector, resultSelector)</c> (.NET 10+)
/// as an intermediate step. Like Join but includes unmatched inner elements with a default outer value.
/// Builds a lookup from the outer (source) collection, then iterates the inner collection.
/// </summary>
public class RightJoinLinqUnroller : BaseLinqUnroller
{
	private const string LookupName = "rightJoinLookup";
	private const string BufferName = "rightJoinBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// TypeArguments: [TOuter, TInner, TKey, TResult]
		var keyType = method.MethodSymbol.TypeArguments[2];
		var outerType = method.MethodSymbol.TypeArguments[0];
		var resultType = method.MethodSymbol.TypeArguments[3];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var outerTypeName = method.Model.Compilation.GetMinimalString(outerType);
		var resultTypeName = method.Model.Compilation.GetMinimalString(resultType);

		// var rightJoinLookup = new Dictionary<TKey, List<TOuter>>();
		statements.Add(CreateLocalDeclaration(LookupName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, List<{outerTypeName}>>"), [])));

		// var rightJoinBuffer = new List<TResult>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{resultTypeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 4
		    || !TryGetLambda(method.Parameters[1], out var outerKeyLambda))
		{
			return;
		}

		var outerKeyExpr = ReplaceLambda(method.Visit(outerKeyLambda) as LambdaExpressionSyntax ?? outerKeyLambda, elementName);

		if (outerKeyExpr is null)
		{
			return;
		}

		var outerType = method.MethodSymbol.TypeArguments[0];
		var outerTypeName = method.Model.Compilation.GetMinimalString(outerType);

		// Build a lookup of outer elements keyed by outerKeySelector
		// var outerKey = outerKeySelector(item);
		statements.Add(CreateLocalDeclaration("outerKey", outerKeyExpr));

		// if (!rightJoinLookup.TryGetValue(outerKey, out var outerList)) { outerList = new List<TOuter>(); rightJoinLookup[outerKey] = outerList; }
		statements.Add(IfStatement(
			LogicalNotExpression(InvocationExpression(
					MemberAccessExpression(IdentifierName(LookupName), IdentifierName("TryGetValue")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(IdentifierName("outerKey")),
					Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("outerList"))))
						.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
				])))),
			Block(
				CreateAssignment("outerList", ObjectCreationExpression(IdentifierName($"List<{outerTypeName}>"), [])),
				ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
					ElementAccessExpression(IdentifierName(LookupName), IdentifierName("outerKey")),
					IdentifierName("outerList"))))));

		// outerList.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName("outerList"), "Add", elementName)));

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length < 4
		    || !TryGetLambda(method.Parameters[2], out var innerKeyLambda)
		    || !TryGetLambda(method.Parameters[3], out var resultLambda))
		{
			return;
		}

		var outerType = method.MethodSymbol.TypeArguments[0];

		// foreach (var innerItem in inner)
		// {
		//     var innerKey = innerKeySelector(innerItem);
		//     if (rightJoinLookup.TryGetValue(innerKey, out var matchedOuters))
		//     {
		//         foreach (var outer in matchedOuters) { var item = resultSelector(outer, innerItem); <partialLoopBody> }
		//     }
		//     else
		//     {
		//         var item = resultSelector(default, innerItem); <partialLoopBody>
		//     }
		// }
		var innerKeyExpr = ReplaceLambda(method.Visit(innerKeyLambda) as LambdaExpressionSyntax ?? innerKeyLambda, IdentifierName("innerItem"));

		if (innerKeyExpr is null)
		{
			return;
		}

		ExpressionSyntax? MakeResult(ExpressionSyntax outerExpr)
		{
			var body = ReplaceLambda(method.Visit(resultLambda) as LambdaExpressionSyntax ?? resultLambda, outerExpr);

			if (body is not null
			    && resultLambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
			{
				var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
				var ids = body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
					.Where(n => n.Identifier.Text == secondParam).ToList();
				body = body.ReplaceNodes(ids, (_, _) => IdentifierName("innerItem"));
			}

			return body;
		}

		var matchedResult = MakeResult(IdentifierName("outer"));
		var defaultResult = MakeResult(outerType.GetDefaultValue());

		if (matchedResult is null || defaultResult is null)
		{
			return;
		}

		var matchedBody = new List<StatementSyntax> { CreateLocalDeclaration("item", matchedResult) };
		matchedBody.AddRange(partialLoopBody);

		var defaultBody = new List<StatementSyntax> { CreateLocalDeclaration("item", defaultResult) };
		defaultBody.AddRange(partialLoopBody);

		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"innerItem",
			method.Parameters[0],
			Block(
				CreateLocalDeclaration("innerKey", innerKeyExpr),
				IfStatement(
					InvocationExpression(
							MemberAccessExpression(IdentifierName(LookupName), IdentifierName("TryGetValue")))
						.WithArgumentList(ArgumentList(SeparatedList([
							Argument(IdentifierName("innerKey")),
							Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("matchedOuters"))))
								.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
						]))),
					Block(ForEachStatement(
						IdentifierName("var"),
						"outer",
						IdentifierName("matchedOuters"),
						Block(matchedBody))),
					ElseClause(Block(defaultBody))))));
	}
}


