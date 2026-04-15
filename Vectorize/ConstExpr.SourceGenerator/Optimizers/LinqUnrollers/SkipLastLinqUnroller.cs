using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.SkipLast(count)</c> as an intermediate step.
/// Uses a <c>Queue&lt;T&gt;</c> buffer of size <c>count</c>; elements are delayed
/// so that the last <c>count</c> elements are never yielded to downstream steps.
/// </summary>
public class SkipLastLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "skipLastBuffer";
	private const string DelayedName = "skipLastDelayed";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var skipLastBuffer = new Queue<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"Queue<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// skipLastBuffer.Enqueue(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Enqueue", elementName)));

		// if (skipLastBuffer.Count <= n) continue;
		statements.Add(IfStatement(
			LessThanOrEqualExpression(
				MemberAccessExpression(IdentifierName(BufferName), IdentifierName("Count")),
				method.Parameters[0]),
			ContinueStatement()));

		// var skipLastDelayed = skipLastBuffer.Dequeue();
		statements.Add(CreateLocalDeclaration(DelayedName,
			CreateMethodInvocation(IdentifierName(BufferName), "Dequeue")));

		elementName = IdentifierName(DelayedName);
	}
}

