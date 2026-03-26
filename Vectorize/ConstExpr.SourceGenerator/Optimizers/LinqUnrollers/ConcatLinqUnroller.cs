using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.Concat(second)</c> as an intermediate step.
/// After the main loop processes the first sequence, a second loop
/// iterates the concatenated sequence through subsequent chain steps.
/// </summary>
public class ConcatLinqUnroller : BaseLinqUnroller
{
	public override void UnrollAfterMainLoop(UnrolledLinqMethod method, IList<StatementSyntax> partialLoopBody, List<StatementSyntax> resultStatements)
	{
		if (method.Parameters.Length < 1)
		{
			return;
		}

		// foreach (var item in secondSequence) { <partialLoopBody> }
		resultStatements.Add(ForEachStatement(
			IdentifierName("var"),
			"item",
			method.Parameters[0],
			Block(partialLoopBody)));
	}
}

