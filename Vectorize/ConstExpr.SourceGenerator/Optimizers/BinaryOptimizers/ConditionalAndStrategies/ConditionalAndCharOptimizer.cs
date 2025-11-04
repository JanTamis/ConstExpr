using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalAndCharOptimizer : CharBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
			&& context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanOrEqualExpression, Right: LiteralExpressionSyntax { Token.Value: 'A' or 'a' or '0' or 'Z' or 'z' or '9' }  } left
			&& context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanOrEqualExpression, Right: LiteralExpressionSyntax { Token.Value: 'A' or 'a' or '0' or 'Z' or 'z' or '9' } } right
			&& left.Left.IsEquivalentTo(right.Left);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax { Right: LiteralExpressionSyntax leftLiteral } left
		    || context.Right.Syntax is not BinaryExpressionSyntax { Right: LiteralExpressionSyntax rightLiteral } right)
		{
			return null;
		}
		
		var memberName = String.Empty;

		switch (leftLiteral.Token.Value)
		{
			case 'A' when rightLiteral.Token.Value is 'Z':
			case 'Z' when rightLiteral.Token.Value is 'A':
				memberName = "IsAsciiLetterUpper";
				break;
			case 'a' when rightLiteral.Token.Value is 'z':
			case 'z' when rightLiteral.Token.Value is 'a':
				memberName = "IsAsciiLetterLower";
				break;
			case '0' when rightLiteral.Token.Value is '9':
			case '9' when rightLiteral.Token.Value is '0':
				memberName = "IsAsciiDigit";
				break;
		}

		if (!String.IsNullOrEmpty(memberName))
		{
			return SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.IdentifierName("Char"),
					SyntaxFactory.IdentifierName(memberName)),
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(left.Left))));
		}
		
		return null;
	}
}