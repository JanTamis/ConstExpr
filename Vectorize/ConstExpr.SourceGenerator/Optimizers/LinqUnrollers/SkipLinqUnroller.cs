using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SkipLinqUnroller : BaseLinqUnroller
{
	private const string SkipCountName = "skipCount";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var skipCount = 0;
		statements.Add(CreateLocalDeclaration(SkipCountName, CreateLiteral(0)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// if (skipCount < n) { skipCount++; continue; }
		statements.Add(IfStatement(BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName(SkipCountName), method.Parameters[0]),
			Block(
				ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(SkipCountName))),
				ContinueStatement())));
	}
}

