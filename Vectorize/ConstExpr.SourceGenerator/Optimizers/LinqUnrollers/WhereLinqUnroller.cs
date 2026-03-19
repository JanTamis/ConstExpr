using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class WhereLinqUnroller : BaseLinqUnroller
{
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var replacedBody = InvertSyntax(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!);

		if (replacedBody.IsKind(SyntaxKind.TrueLiteralExpression))
		{
			statements.Add(ContinueStatement());
		}
		else if (!replacedBody.IsKind(SyntaxKind.FalseLiteralExpression))
		{
			statements.Add(IfStatement(replacedBody, ContinueStatement()));
		}
	}
}