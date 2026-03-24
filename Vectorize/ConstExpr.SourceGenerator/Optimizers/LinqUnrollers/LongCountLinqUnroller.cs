using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class LongCountLinqUnroller : BaseLinqUnroller
{
	private const string ResultName = "result";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// var result = 0L;
		statements.Add(CreateLocalDeclaration(ResultName, CreateLiteral(0L)!));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.Parameters.Length == 1
		    && TryGetLambda(method.Parameters[0], out var lambda))
		{
			statements.Add(IfStatement(InvertSyntax(ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, elementName)!),
				ContinueStatement()));
		}

		// result++;
		statements.Add(ExpressionStatement(PostIncrementExpression(IdentifierName(ResultName))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		statements.Add(ReturnStatement(IdentifierName(ResultName)));
	}
}


