using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class TakeLinqUnroller : BaseLinqUnroller
{
	private const string TakeCountName = "takeCount";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var takeCount = 0;
		statements.Add(CreateLocalDeclaration(TakeCountName, CreateLiteral(0)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// if (takeCount >= n) break;
		statements.Add(IfStatement(
			BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, IdentifierName(TakeCountName), method.Parameters[0]),
			BreakStatement()));

		// takeCount++;
		statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(TakeCountName))));
	}
}

