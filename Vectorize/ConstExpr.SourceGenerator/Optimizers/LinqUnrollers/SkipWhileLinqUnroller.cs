using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SkipWhileLinqUnroller : BaseLinqUnroller
{
	private const string SkippingName = "skipping";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var skipping = true;
		statements.Add(CreateLocalDeclaration(SkippingName, CreateLiteral(true)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var predicateBody = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!;

		// if (skipping) { if (predicate(item)) continue; skipping = false; }
		statements.Add(IfStatement(IdentifierName(SkippingName),
			Block(
				IfStatement(predicateBody, 
					ContinueStatement()),
				CreateAssignment(SkippingName, CreateLiteral(false)!))));
	}
}

