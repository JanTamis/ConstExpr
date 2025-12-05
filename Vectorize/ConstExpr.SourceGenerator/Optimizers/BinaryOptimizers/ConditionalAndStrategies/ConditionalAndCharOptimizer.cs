using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalAndCharOptimizer : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanOrEqualExpression, Right: LiteralExpressionSyntax { Token.Value: char } } left
			&& context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanOrEqualExpression, Right: LiteralExpressionSyntax { Token.Value: char } } right
			&& left.Left.IsEquivalentTo(right.Left);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax { Right: LiteralExpressionSyntax leftLiteral } left
		    || context.Right.Syntax is not BinaryExpressionSyntax { Right: LiteralExpressionSyntax rightLiteral } right)
		{
			return null;
		}

		var memberName = (leftLiteral.Token.Value, rightLiteral.Token.Value) switch
		{
			('A', 'Z') => "IsAsciiLetterUpper",
			('a', 'z') => "IsAsciiLetterLower",
			('0', '9') => "IsAsciiDigit",
			_ => String.Empty
		};

		if (!String.IsNullOrEmpty(memberName))
		{
			return InvocationExpression(
        MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
          IdentifierName("Char"),
          IdentifierName(memberName)),
        ArgumentList(
          SingletonSeparatedList(
            Argument(left.Left))));
		}

		if (ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, leftLiteral.Token.Value, rightLiteral.Token.Value) is true)
		{
			return InvocationExpression(
        MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
          IdentifierName("Char"),
          IdentifierName("IsBetween")),
        ArgumentList([Argument(left.Left), Argument(leftLiteral), Argument(rightLiteral)]));
		}

		return null;
	}
}