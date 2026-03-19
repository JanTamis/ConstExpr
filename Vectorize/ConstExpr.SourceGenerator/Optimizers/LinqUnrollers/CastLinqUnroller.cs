using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class CastLinqUnroller : BaseLinqUnroller
{
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
}