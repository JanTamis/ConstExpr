using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

public class ConditionalOrIsNullOrEmptyStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } leftExpr
		       && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } rightExpr
		       && IsNullCheck(leftExpr)
		       && IsEmptyStringCheck(rightExpr);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return InvocationExpression(
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName("String"),
				IdentifierName("IsNullOrEmpty")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax is BinaryExpressionSyntax leftExpr && IsNullCheck(leftExpr)
							? GetExpressionSyntax(leftExpr)
							: context.Right.Syntax is BinaryExpressionSyntax rightExpr && IsNullCheck(rightExpr)
								? GetExpressionSyntax(rightExpr)
								: throw new InvalidOperationException()))));
	}

	private bool IsNullCheck(BinaryExpressionSyntax expr)
	{
		return expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression }
		       || expr.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression };
	}

	private bool IsEmptyStringCheck(BinaryExpressionSyntax expr)
	{
		return (expr.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" }
		        || expr.Right is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" })
		       && (expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NumericLiteralExpression, Token.Value: 0 }
		           || expr.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NumericLiteralExpression, Token.Value: 0 });
	}
	
	private ExpressionSyntax GetExpressionSyntax(BinaryExpressionSyntax expr)
	{
		return expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression }
			? expr.Right
			: expr.Left;
	}
}