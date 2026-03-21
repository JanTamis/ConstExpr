using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class TakeWhileLinqUnroller : BaseLinqUnroller
{
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var predicateBody = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!;

		// if (!predicate(item)) break;
		statements.Add(IfStatement(InvertSyntax(predicateBody), 
			BreakStatement()));
	}
}

