using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class CastLinqUnroller : BaseLinqUnroller
{
	public override void UnrollAboveLoop(UnrolledLinqMethod method, IMethodSymbol methodSymbol, List<StatementSyntax> statements)
	{
		
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.TypeArguments.Length != 1)
		{
			return;
		}

		var replacedBody = CastExpression(method.TypeArguments[0], elementName);
		var newName = $"item_{replacedBody.GetDeterministicHashString()}";

		elementName = IdentifierName(newName);

		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(newName).WithInitializer(EqualsValueClause(replacedBody))))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statementSyntaxes)
	{
		
	}
}