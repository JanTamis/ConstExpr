using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Chunk(size)</c> as an intermediate step.
/// Collects elements into a temporary list, flushing as a sub-array of <c>size</c> elements
/// through downstream chain steps each time the chunk is full.
/// Remaining elements are flushed after the main loop.
/// </summary>
public class ChunkLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "chunkBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var chunkBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// chunkBuffer.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", elementName)));

		// continue; — flush happens after main loop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		var chunkSize = method.Parameters[0];

		// Process full chunks and remainder from the buffer:
		// for (var chunkStart = 0; chunkStart < chunkBuffer.Count; chunkStart += size)
		// {
		//     var count = Math.Min(size, chunkBuffer.Count - chunkStart);
		//     var item = chunkBuffer.GetRange(chunkStart, count).ToArray();
		//     <partialLoopBody>
		// }
		var chunkStartName = "chunkStart";
		var chunkCountName = "chunkCount";

		var body = new List<StatementSyntax>
		{
			// var chunkCount = Math.Min(size, chunkBuffer.Count - chunkStart);
			CreateLocalDeclaration(chunkCountName,
				InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("Math"), IdentifierName("Min")))
					.WithArgumentList(ArgumentList(SeparatedList([
						Argument(chunkSize),
						Argument(SubtractExpression(
							MemberAccessExpression(IdentifierName(BufferName), IdentifierName("Count")),
							IdentifierName(chunkStartName)))
					])))),
			// var item = chunkBuffer.GetRange(chunkStart, chunkCount).ToArray();
			CreateLocalDeclaration("item",
				CreateMethodInvocation(
					CreateMethodInvocation(IdentifierName(BufferName), "GetRange", IdentifierName(chunkStartName), IdentifierName(chunkCountName)),
					"ToArray"))
		};
		body.AddRange(partialLoopBody);

		resultStatements.Add(ForStatement(Block(body))
			.WithDeclaration(VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(chunkStartName)
						.WithInitializer(EqualsValueClause(CreateLiteral(0)!)))))
			.WithCondition(LessThanExpression(
				IdentifierName(chunkStartName),
				MemberAccessExpression(IdentifierName(BufferName), IdentifierName("Count"))))
			.WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(
				AssignmentExpression(SyntaxKind.AddAssignmentExpression,
					IdentifierName(chunkStartName), chunkSize))));
	}
}


