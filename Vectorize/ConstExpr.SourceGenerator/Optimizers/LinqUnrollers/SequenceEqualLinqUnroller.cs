using System.Collections.Generic;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

/// <summary>
/// Unrolls <c>.SequenceEqual(other)</c> as a terminal step.
/// Iterates source and <c>other</c> in lock-step via an enumerator, comparing each pair.
/// Returns <c>false</c> immediately on any mismatch or length difference.
/// </summary>
public class SequenceEqualLinqUnroller : BaseLinqUnroller
{
	private const string EnumeratorName = "seqEnum";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		if (method.Parameters.Length < 1)
			return;

		// using var seqEnum = other.GetEnumerator();
		statements.Add(CreateLocalDeclaration(EnumeratorName, CreateMethodInvocation(method.Parameters[0], "GetEnumerator"))
			.WithUsingKeyword(Token(SyntaxKind.UsingKeyword)));
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		var moveNext = CreateMethodInvocation(IdentifierName(EnumeratorName), "MoveNext");
		var current = MemberAccessExpression(IdentifierName(EnumeratorName), IdentifierName("Current"));

		// if (!seqEnum.MoveNext()) return false;
		statements.Add(IfStatement(
			LogicalNotExpression(moveNext),
			ReturnStatement(CreateLiteral(false))));

		// if (item != seqEnum.Current) return false;
		statements.Add(IfStatement( NotEqualsExpression(elementName, current),
			ReturnStatement(CreateLiteral(false))));
	}

	public override void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// return !seqEnum.MoveNext(); — true only when both sequences are the same length
		var moveNext = CreateMethodInvocation(IdentifierName(EnumeratorName), "MoveNext");

		statements.Add(ReturnStatement(LogicalNotExpression(moveNext)));
	}
}