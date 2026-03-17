using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class ContainsLinqUnroller : BaseLinqUnroller
{
	public override void UnrollAboveLoop(UnrolledLinqMethod method, IMethodSymbol methodSymbol, List<StatementSyntax> statements)
	{
		
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax?> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		statements.Add(IfStatement(BinaryExpression(SyntaxKind.NotEqualsExpression, elementName, method.Parameters[0]), ContinueStatement()));
		statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statementSyntaxes)
	{
		
	}
}