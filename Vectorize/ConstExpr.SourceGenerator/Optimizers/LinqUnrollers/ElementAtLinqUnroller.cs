using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class ElementAtLinqUnroller : BaseLinqUnroller
{
	private const string IndexCountName = "indexCount";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var indexCount = index;
		statements.Add(CreateLocalDeclaration(IndexCountName, method.Parameters[0]));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		// if (indexCount == 0) return item;
		statements.Add(IfStatement(EqualsExpression(IdentifierName(IndexCountName), CreateLiteral(0)!),
			ReturnStatement(elementName)));

		// indexCount--;
		statements.Add(ExpressionStatement(PostDecrementExpression(IdentifierName(IndexCountName))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// throw new ArgumentOutOfRangeException("index");
		statements.Add(CreateThrowExpression<ArgumentOutOfRangeException>("index"));
	}
}