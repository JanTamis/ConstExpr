using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class SelectLinqUnroller : BaseLinqUnroller 
{
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
}