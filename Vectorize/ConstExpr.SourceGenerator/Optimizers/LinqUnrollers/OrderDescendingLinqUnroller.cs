using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.OrderDescending()</c> as an intermediate step.
/// Collects all elements into a list during the main loop, sorts it,
/// then iterates in reverse (descending) order through subsequent chain steps.
/// </summary>
public class OrderDescendingLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "orderDescBuffer";
	private const string IndexName = "orderDescIdx";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var orderDescBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// orderDescBuffer.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", elementName)));

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// orderDescBuffer.Sort();
		resultStatements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Sort")));

		// Iterate in reverse for descending order
		// for (var orderDescIdx = orderDescBuffer.Count - 1; orderDescIdx >= 0; orderDescIdx--)
		// {
		//     var item = orderDescBuffer[orderDescIdx];
		//     <partialLoopBody>
		// }
		var body = new List<StatementSyntax>
		{
			CreateLocalDeclaration("item", ElementAccessExpression(IdentifierName(BufferName), IdentifierName(IndexName)))
		};
		body.AddRange(partialLoopBody);

		resultStatements.Add(ForStatement(Block(body))
			.WithDeclaration(VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(IndexName)
						.WithInitializer(EqualsValueClause(
							SubtractExpression(
								MemberAccessExpression(IdentifierName(BufferName), IdentifierName("Count")),
								CreateLiteral(1)!))))))
			.WithCondition(GreaterThanOrEqualExpression(IdentifierName(IndexName), CreateLiteral(0)!))
			.WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(
				PostDecrementExpression(IdentifierName(IndexName)))));
	}
}

