using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class AnyLinqUnroller : BaseLinqUnroller
{
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1
		    && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(IfStatement(InvertSyntax(ReplaceLambda(lambda, elementName)!), ContinueStatement()));
		}

		statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));
	}
}