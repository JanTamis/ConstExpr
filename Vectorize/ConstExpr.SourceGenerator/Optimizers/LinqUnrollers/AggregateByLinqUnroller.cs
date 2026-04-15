using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.AggregateBy(keySelector, seed, func)</c> as an intermediate step.
/// Builds a <c>Dictionary&lt;TKey, TAccumulate&gt;</c> during the main loop by
/// applying the accumulator function per key, then iterates the dictionary
/// through subsequent chain steps after the main loop.
/// </summary>
public class AggregateByLinqUnroller : BaseLinqUnroller
{
	private const string DictName = "aggregateByDict";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var keyType = method.MethodSymbol.TypeArguments[1];
		var accType = method.MethodSymbol.TypeArguments[2];

		var keyTypeName = method.Model.Compilation.GetMinimalString(keyType);
		var accTypeName = method.Model.Compilation.GetMinimalString(accType);

		// var aggregateByDict = new Dictionary<TKey, TAccumulate>();
		statements.Add(CreateLocalDeclaration(DictName,
			ObjectCreationExpression(IdentifierName($"Dictionary<{keyTypeName}, {accTypeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// AggregateBy(keySelector, seed, func) — Parameters: [keySelector, seed, func]
		if (method.Parameters.Length < 3
		    || !TryGetLambda(method.Parameters[0], out var keyLambda)
		    || !TryGetLambda(method.Parameters[2], out var funcLambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(keyLambda) as LambdaExpressionSyntax ?? keyLambda, elementName);

		if (keyExpr is null)
		{
			return;
		}

		// var aggKey = keySelector(item);
		statements.Add(CreateLocalDeclaration("aggKey", keyExpr));

		// if (!aggregateByDict.TryGetValue(aggKey, out var aggValue)) aggValue = seed;
		statements.Add(IfStatement(
			LogicalNotExpression(InvocationExpression(
					MemberAccessExpression(IdentifierName(DictName), IdentifierName("TryGetValue")))
				.WithArgumentList(ArgumentList(SeparatedList([
					Argument(IdentifierName("aggKey")),
					Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier("aggValue"))))
						.WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
				])))),
			CreateAssignment("aggValue", method.Parameters[1])));

		// aggregateByDict[aggKey] = func(aggValue, item);
		// Replace first lambda param (accumulator) with aggValue, second (element) with item
		var funcBody = ReplaceLambda(method.Visit(funcLambda) as LambdaExpressionSyntax ?? funcLambda, IdentifierName("aggValue"));

		if (funcBody is not null
		    && funcLambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: >= 2 } pl)
		{
			var secondParam = pl.ParameterList.Parameters[1].Identifier.Text;
			var element = elementName;
			var identifiers = funcBody
				.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Where(n => n.Identifier.Text == secondParam)
				.ToList();

			funcBody = funcBody.ReplaceNodes(identifiers, (_, _) => element);
		}

		if (funcBody is not null)
		{
			statements.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
				ElementAccessExpression(IdentifierName(DictName), IdentifierName("aggKey")),
				funcBody)));
		}

		// continue; — don't process further in the main loop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in aggregateByDict) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(DictName),
			Block(partialLoopBody)));
	}
}


