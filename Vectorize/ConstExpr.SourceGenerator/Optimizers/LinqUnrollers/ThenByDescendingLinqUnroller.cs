using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.ThenByDescending(keySelector)</c> as an intermediate step.
/// Collects all elements into a list during the main loop, sorts by the key selector
/// in descending order, then iterates through subsequent chain steps after the main loop.
/// </summary>
public class ThenByDescendingLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "thenByDescBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var thenByDescBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// thenByDescBuffer.Add(item);
		statements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", elementName)));

		// continue;
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length < 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		// Descending sort: compare keyB to keyA
		var paramA = "thenDescSortA";
		var paramB = "thenDescSortB";

		var keyA = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(paramA));
		var keyB = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(paramB));

		if (keyA is null || keyB is null)
		{
			return;
		}

		var comparer = ParenthesizedLambdaExpression()
			.WithParameterList(ParameterList(SeparatedList([
				Parameter(Identifier(paramA)),
				Parameter(Identifier(paramB))
			])))
			.WithExpressionBody(CreateMethodInvocation(keyB, "CompareTo", keyA));

		resultStatements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Sort", comparer)));

		// foreach (var item in thenByDescBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

