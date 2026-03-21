using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class MinByLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";
	private const string BestKeyName = "bestKey";
	private const string FirstName = "first";
	private const string KeyName = "key";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var result = default(T);
		statements.Add(CreateLocalDeclaration(ResultName, method.MethodSymbol.ReturnType.GetDefaultValue()));

		// var bestKey = default(TKey);
		statements.Add(CreateLocalDeclaration(BestKeyName, method.MethodSymbol.TypeArguments[^1].GetDefaultValue()));

		// var first = true;
		statements.Add(CreateLocalDeclaration(FirstName, CreateLiteral(true)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length < 1
		    || !TryGetLambda(method.Parameters[0], out var lambda))
		{
			return;
		}

		var keyExpr = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!;

		// var key = selector(item);
		statements.Add(CreateLocalDeclaration(KeyName, keyExpr));

		// if (first || key < bestKey) { result = item; bestKey = key; first = false; }
		var condition = BinaryExpression(SyntaxKind.LogicalOrExpression,
			IdentifierName(FirstName),
			BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName(KeyName), IdentifierName(BestKeyName)));

		statements.Add(IfStatement(condition, Block(
			CreateAssignment(ResultName, elementName),
			CreateAssignment(BestKeyName, IdentifierName(KeyName)),
			CreateAssignment(FirstName, CreateLiteral(false)))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


