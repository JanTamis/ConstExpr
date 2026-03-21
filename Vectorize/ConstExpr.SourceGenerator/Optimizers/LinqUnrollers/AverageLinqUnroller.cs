using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class AverageLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string CountName = "count";
	
	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(CreateLocalDeclaration(ResultName, method.MethodSymbol.ReturnType.GetDefaultValue()));
		statements.Add(CreateLocalDeclaration(CountName, CreateLiteral(0)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(ExpressionStatement(AssignmentExpression(
				SyntaxKind.AddAssignmentExpression,
				IdentifierName(ResultName),
				ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!)));
		}
		else
		{
			statements.Add(ExpressionStatement(AssignmentExpression(
				SyntaxKind.AddAssignmentExpression,
				IdentifierName(ResultName),
				elementName)));
		}
		
		statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(CountName))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(BinaryExpression(SyntaxKind.DivideExpression, IdentifierName(ResultName), IdentifierName(CountName))));
	}
}