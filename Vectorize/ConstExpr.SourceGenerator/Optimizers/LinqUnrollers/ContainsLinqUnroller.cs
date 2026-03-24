using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class ContainsLinqUnroller : BaseLinqUnroller
{
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		statements.Add(IfStatement(NotEqualsExpression(elementName, method.Parameters[0]), 
			ContinueStatement()));
		
		statements.Add(ReturnStatement(CreateLiteral(true)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(CreateLiteral(false)));
	}
}