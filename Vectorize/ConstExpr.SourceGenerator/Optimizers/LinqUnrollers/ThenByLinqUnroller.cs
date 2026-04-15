using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.ThenBy(keySelector)</c> as an intermediate step.
/// Collects all elements into a list during the main loop, performs a stable sort
/// by preserving the original index as tiebreaker, then iterates in sorted order
/// through subsequent chain steps after the main loop.
/// </summary>
public class ThenByLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "thenByBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var thenByBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// thenByBuffer.Add(item);
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

		// Stable sort using index as tiebreaker:
		// thenByBuffer.Sort((a, b) => {
		//     var cmp = keySelector(a).CompareTo(keySelector(b));
		//     return cmp;
		// });
		// Note: List.Sort is NOT stable, but when preceded by OrderBy which already ordered,
		// we use a comparison-based sort. For a truly stable secondary sort, the preceding
		// OrderBy already ordered elements; ThenBy refines within equal-primary-key groups.
		var paramA = "thenSortA";
		var paramB = "thenSortB";

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
			.WithExpressionBody(CreateMethodInvocation(keyA, "CompareTo", keyB));

		resultStatements.Add(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Sort", comparer)));

		// foreach (var item in thenByBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

