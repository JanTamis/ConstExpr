using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MinLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string FirstName = "first";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var result = default(T);
		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(ResultName).WithInitializer(EqualsValueClause(method.MethodSymbol.ReturnType.GetDefaultValue()))))));

		// var first = true;
		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(FirstName).WithInitializer(EqualsValueClause(CreateLiteral(true)))))));
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

		// if (first || value < result) { result = value; first = false; }
		var condition = BinaryExpression(SyntaxKind.LogicalOrExpression,
			IdentifierName(FirstName),
			BinaryExpression(SyntaxKind.LessThanExpression, value, IdentifierName(ResultName)));

		statements.Add(IfStatement(condition, Block(
			ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(ResultName), value)),
			ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(FirstName), CreateLiteral(false))))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


