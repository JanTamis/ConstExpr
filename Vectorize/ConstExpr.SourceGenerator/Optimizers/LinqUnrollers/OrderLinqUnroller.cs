using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Order()</c> as an intermediate step.
/// Collects all elements into a list during the main loop, sorts it,
/// then iterates in sorted order through subsequent chain steps after the main loop.
/// </summary>
public class OrderLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "orderBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var orderBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// orderBuffer.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", elementName)));

		// continue; — don't process further in the main loop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// orderBuffer.Sort();
		resultStatements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Sort")));

		// foreach (var item in orderBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

