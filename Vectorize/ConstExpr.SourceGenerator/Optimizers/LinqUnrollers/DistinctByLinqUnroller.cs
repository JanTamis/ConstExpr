using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class DistinctByLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "distinctBySet";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(CreateLocalDeclaration(SetName, ObjectCreationExpression(IdentifierName($"HashSet<{method.Model.Compilation.GetMinimalString(method.MethodSymbol.TypeArguments[^1])}>"))
			.WithArgumentList(ArgumentList())));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(IfStatement(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
				CreateMethodInvocation(IdentifierName(SetName), "Add", ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(elementName.ToString())))), 
				ContinueStatement()));
		}
	}
}