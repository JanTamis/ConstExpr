using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SelectLinqUnroller : BaseLinqUnroller 
{
	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statement)
	{
		
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length != 1)
		{
			return;
		}

		var predicate = method.Parameters[0];

		if (!TryGetLambda(method.Visit(predicate) as ExpressionSyntax, out var lambda))
		{
			return;
		}

		var replacedBody = ReplaceLambda(lambda, elementName);
		var newName = $"item_{replacedBody!.GetDeterministicHashString()}";

		elementName = IdentifierName(newName);

		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(SingletonSeparatedList(VariableDeclarator(newName).WithInitializer(EqualsValueClause(replacedBody))))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		
	}
}