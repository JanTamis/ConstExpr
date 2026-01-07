using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

public class ConditionalOrIsNullOrEmptyStrategy : SymmetricStrategy<BooleanBinaryStrategy, BinaryExpressionSyntax, BinaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.EqualsExpression)
		    || !context.Right.Syntax.IsKind(SyntaxKind.EqualsExpression)
		    || !IsNullCheck(context.Left.Syntax)
		    || !IsEmptyStringCheck(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName("String"),
				IdentifierName("IsNullOrEmpty")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(GetExpressionSyntax(context.Left.Syntax)))));
		return true;
	}

	private static bool IsNullCheck(BinaryExpressionSyntax expr)
	{
		return expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression }
		       || expr.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression };
	}

	private static bool IsEmptyStringCheck(BinaryExpressionSyntax expr)
	{
		return (expr.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" }
		        || expr.Right is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" })
		       && (expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NumericLiteralExpression, Token.Value: 0 }
		           || expr.Right is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NumericLiteralExpression, Token.Value: 0 });
	}
	
	private static ExpressionSyntax GetExpressionSyntax(BinaryExpressionSyntax expr)
	{
		return expr.Left is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression }
			? expr.Right
			: expr.Left;
	}
}