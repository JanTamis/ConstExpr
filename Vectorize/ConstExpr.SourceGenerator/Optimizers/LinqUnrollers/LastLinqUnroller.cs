using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class LastLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string FoundName = "found";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var result = default(T);
		statements.Add(CreateLocalDeclaration(ResultName, method.MethodSymbol.ReturnType.GetDefaultValue()));

		// var found = false;
		statements.Add(CreateLocalDeclaration(FoundName, CreateLiteral(false)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1
		    && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(IfStatement(InvertSyntax(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!), 
				ContinueStatement()));
		}

		statements.Add(CreateAssignment(ResultName, elementName));
		statements.Add(CreateAssignment(FoundName, CreateLiteral(true)!));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// if (!found) throw new InvalidOperationException(...);
		statements.Add(IfStatement(LogicalNotExpression(IdentifierName(FoundName)), 
			CreateThrowExpression<InvalidOperationException>("Sequence contains no matching element")));

		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


