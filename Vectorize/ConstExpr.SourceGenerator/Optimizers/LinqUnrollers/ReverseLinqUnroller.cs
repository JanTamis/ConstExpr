using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Reverse()</c> as an intermediate step.
/// Collects all elements into a list during the main loop, then iterates
/// the list in reverse order through subsequent chain steps after the main loop.
/// </summary>
public class ReverseLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "reverseBuffer";
	private const string IndexName = "reverseIdx";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var reverseBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// reverseBuffer.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", elementName)));

		// continue; — don't process further in the main loop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// for (var reverseIdx = reverseBuffer.Count - 1; reverseIdx >= 0; reverseIdx--)
		// {
		//     var item = reverseBuffer[reverseIdx];
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

