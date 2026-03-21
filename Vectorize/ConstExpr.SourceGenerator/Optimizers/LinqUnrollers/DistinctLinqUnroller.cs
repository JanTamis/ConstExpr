using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class DistinctLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "distinctSet";
	
	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(CreateLocalDeclaration(SetName, ObjectCreationExpression(IdentifierName($"HashSet<{ method.Model.Compilation.GetMinimalString(method.MethodSymbol.TypeArguments[0])}>"))
			.WithArgumentList(ArgumentList())));
	}
	
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		statements.Add(IfStatement(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
			CreateMethodInvocation(IdentifierName(SetName), "Add", IdentifierName(elementName.ToString()))), 
			ContinueStatement()));
	}
}