using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.TakeLast(count)</c> as an intermediate step.
/// Uses a <c>Queue&lt;T&gt;</c> as a ring buffer during the main loop; elements
/// are buffered and then processed through downstream chain steps after the main loop.
/// </summary>
public class TakeLastLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "takeLastBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var takeLastBuffer = new Queue<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"Queue<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// takeLastBuffer.Enqueue(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Enqueue", elementName)));

		// if (takeLastBuffer.Count > n) takeLastBuffer.Dequeue();
		statements.Add(IfStatement(
			GreaterThanExpression(
				MemberAccessExpression(IdentifierName(BufferName), IdentifierName("Count")),
				method.Parameters[0]),
			ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Dequeue"))));

		// continue; — don't process further in the main loop; wait for AfterMainLoop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in takeLastBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

