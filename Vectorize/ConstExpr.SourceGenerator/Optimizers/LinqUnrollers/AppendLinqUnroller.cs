using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Append(element)</c> as an intermediate step.
/// The appended element is processed through subsequent chain steps
/// after the main loop completes, preserving correct LINQ semantics
/// where Append adds an element after the current sequence position.
/// </summary>
public class AppendLinqUnroller : BaseLinqUnroller
{
	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// foreach (var item in new[] { appendedElement }) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			ImplicitArrayCreationExpression(
				InitializerExpression(SyntaxKind.ArrayInitializerExpression,
					SingletonSeparatedList(method.Parameters[0]))),
			Block(partialLoopBody)));
	}
}

