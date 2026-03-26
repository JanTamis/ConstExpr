using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.UnionBy(second, keySelector)</c> as an intermediate step.
/// Uses a <c>HashSet&lt;TKey&gt;</c> to deduplicate elements by projected key
/// across both sequences. The first sequence is deduplicated in the main loop,
/// then the second sequence is processed after the main loop with the same set.
/// </summary>
public class UnionByLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "unionBySet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 2)
		{
			return;
		}

		var keyType = method.MethodSymbol.TypeArguments[^1];
		var typeName = method.Model.Compilation.GetMinimalString(keyType);

		// var unionBySet = new HashSet<TKey>();
		statements.Add(CreateLocalDeclaration(SetName,
			ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), [])));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 2
		    || !TryGetLambda(method.Parameters[1], out var lambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName);

		if (keyExpr is null)
		{
			return;
		}

		// if (!unionBySet.Add(keySelector(item))) continue;
		statements.Add(IfStatement(
			LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", keyExpr)),
			ContinueStatement()));
	}

	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length < 2
		    || !TryGetLambda(method.Parameters[1], out var lambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName("item"));

		if (keyExpr is null)
		{
			return;
		}

		// Prepend the key-based dedup check for the second sequence
		var bodyWithDedup = new List<StatementSyntax>
		{
			IfStatement(
				LogicalNotExpression(CreateMethodInvocation(IdentifierName(SetName), "Add", keyExpr)),
				ContinueStatement())
		};
		bodyWithDedup.AddRange(partialLoopBody);

		// foreach (var item in secondSequence) { if (!unionBySet.Add(keySelector(item))) continue; <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			method.Parameters[0],
			Block(bodyWithDedup)));
	}
}

