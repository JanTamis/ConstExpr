using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MaxLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string FirstName = "first";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var result = default(T);
		statements.Add(CreateLocalDeclaration(ResultName, method.MethodSymbol.ReturnType.GetDefaultValue()));

		// var first = true;
		statements.Add(CreateLocalDeclaration(FirstName, IdentifierName("var"), CreateLiteral(true)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		ExpressionSyntax value;

		if (method.Parameters.Length == 1
		    && TryGetLambda(method.Parameters[0], out var lambda))
		{
			value = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!;
		}
		else
		{
			value = elementName;
		}

		// if (first || value > result) { result = value; first = false; }
		var condition = BinaryExpression(SyntaxKind.LogicalOrExpression,
			IdentifierName(FirstName),
			BinaryExpression(SyntaxKind.GreaterThanExpression, value, IdentifierName(ResultName)));

		statements.Add(IfStatement(condition, Block(
			CreateAssignment(ResultName, value),
			CreateAssignment(FirstName, CreateLiteral(false)))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


