using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.OrderBy(keySelector)</c> as an intermediate step.
/// Collects all elements into a list during the main loop, sorts by the key selector,
/// then iterates in sorted order through subsequent chain steps after the main loop.
/// </summary>
public class OrderByLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "orderByBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var orderByBuffer = new List<T>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// orderByBuffer.Add(item);
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

		// orderByBuffer.Sort((a, b) => keySelector(a).CompareTo(keySelector(b)));
		var paramA = "sortA";
		var paramB = "sortB";

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

		// foreach (var item in orderByBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}


