using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Prepend(element)</c> as an intermediate step.
/// The prepended element is processed through subsequent chain steps
/// before the main loop starts, preserving correct LINQ semantics
/// where Prepend adds an element before the current sequence position.
/// </summary>
public class PrependLinqUnroller : BaseLinqUnroller
{
	public override void UnrollBeforeMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// foreach (var item in new[] { prependedElement }) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			ImplicitArrayCreationExpression(
				InitializerExpression(SyntaxKind.ArrayInitializerExpression,
					SingletonSeparatedList(method.Parameters[0]))),
			Block(partialLoopBody)));
	}
}

