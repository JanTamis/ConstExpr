using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class DistinctLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "distinctSet";
	
	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
			.WithVariables(
				SingletonSeparatedList(VariableDeclarator(SetName)
					.WithInitializer(EqualsValueClause(ObjectCreationExpression(IdentifierName($"HashSet<{method.MethodSymbol.TypeArguments[0].ToDisplayString()}>"))
						.WithArgumentList(ArgumentList())))))));
	}
	
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		statements.Add(IfStatement(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(SetName), IdentifierName("Add")))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(elementName.ToString())))))), ContinueStatement()));
	}
}