using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SingleOrDefaultLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string FoundName = "found";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(CreateLocalDeclaration(ResultName, method.MethodSymbol.ReturnType.GetDefaultValue()));
		statements.Add(CreateLocalDeclaration(FoundName, CreateLiteral(false)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1
		    && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(IfStatement(InvertSyntax(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!), 
				ContinueStatement()));
		}

		// if (found) throw new InvalidOperationException("Sequence contains more than one matching element");
		statements.Add(IfStatement(IdentifierName(FoundName), 
			CreateThrowExpression<InvalidOperationException>("Sequence contains more than one matching element")));

		statements.Add(CreateAssignment(ResultName, elementName));
		statements.Add(CreateAssignment(FoundName, CreateLiteral(true)));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}