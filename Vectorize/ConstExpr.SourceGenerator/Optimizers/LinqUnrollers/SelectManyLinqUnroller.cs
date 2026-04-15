using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.SelectMany(selector)</c> as an intermediate step.
/// Collects all flattened inner elements into a list during the main loop,
/// then processes them through subsequent chain steps after the main loop.
/// </summary>
public class SelectManyLinqUnroller : BaseLinqUnroller
{
	private const string BufferName = "selectManyBuffer";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } named
			? named.TypeArguments[0]
			: method.MethodSymbol.TypeArguments[^1];

		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var selectManyBuffer = new List<TResult>();
		statements.Add(CreateLocalDeclaration(BufferName,
			ObjectCreationExpression(IdentifierName($"List<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var subCollection = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName);

		if (subCollection is null)
		{
			return;
		}

		// foreach (var inner in selector(item)) { selectManyBuffer.Add(inner); }
		statements.Add(ForEachStatement(
			IdentifierName("var"),
			"inner",
			subCollection,
			Block(ExpressionStatement(CreateMethodInvocation(IdentifierName(BufferName), "Add", IdentifierName("inner"))))));

		// continue; — skip downstream processing in the main loop
		statements.Add(ContinueStatement());
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		// foreach (var item in selectManyBuffer) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			IdentifierName(BufferName),
			Block(partialLoopBody)));
	}
}

