using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SumLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	
	public override void UnrollAboveLoop(UnrolledLinqMethod method, IMethodSymbol methodSymbol, List<StatementSyntax> statements)
	{
		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(ResultName).WithInitializer(EqualsValueClause(methodSymbol.ReturnType.GetDefaultValue()))))));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1 && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.AddAssignmentExpression, IdentifierName(ResultName), ReplaceLambda(lambda, elementName)!)));
		}
		else
		{
			statements.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.AddAssignmentExpression, IdentifierName(ResultName), elementName)));
		}
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}