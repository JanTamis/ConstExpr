using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Union(second)</c> as an intermediate step.
/// Combines two sequences and eliminates duplicates using a <c>HashSet&lt;T&gt;</c>.
/// The first sequence is deduplicated in the main loop, then the second
/// sequence is processed after the main loop with the same dedup set.
/// </summary>
public class UnionLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "unionSet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1)
		{
			return;
		}

		var elementType = method.MethodSymbol.TypeArguments[0];
		var typeName = method.Model.Compilation.GetMinimalString(elementType);

		// var unionSet = new HashSet<T>();
		statements.Add(CreateLocalDeclaration(SetName,
			ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		// if (!unionSet.Add(item)) continue;
		statements.Add(IfStatement(
			LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", elementName)),
			ContinueStatement()));
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length < 1)
		{
			return;
		}

		// The partial body starts from the step AFTER Union, so it doesn't include
		// the dedup check. We prepend the dedup check for the second sequence too.
		var bodyWithDedup = new List<StatementSyntax>
		{
			IfStatement(
				LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", IdentifierName("item"))),
				ContinueStatement())
		};
		bodyWithDedup.AddRange(partialLoopBody);

		// foreach (var item in secondSequence) { if (!unionSet.Add(item)) continue; <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			method.Parameters[0],
			Block(bodyWithDedup)));
	}
}

