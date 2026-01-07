using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalAndCharOptimizer : SymmetricStrategy<BooleanBinaryStrategy, BinaryExpressionSyntax, BinaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
		    || !context.Right.Syntax.IsKind(SyntaxKind.LessThanOrEqualExpression)
		    || !LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax.Left, context.TryGetValue)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftValue)
		    || !context.TryGetValue(context.Right.Syntax.Right, out var rightValue))
		{
			optimized = null;
			return false;
		}

		var memberName = (leftValue, rightValue) switch
		{
			('A', 'Z') => "IsAsciiLetterUpper",
			('a', 'z') => "IsAsciiLetterLower",
			('0', '9') => "IsAsciiDigit",
			_ => String.Empty
		};

		if (!String.IsNullOrEmpty(memberName))
		{
			optimized =  InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					IdentifierName("Char"),
					IdentifierName(memberName)),
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax.Left))));
			
			return true;
		}

		if (leftValue.LessThan(rightValue))
		{
			optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					IdentifierName("Char"),
					IdentifierName("IsBetween")),
				ArgumentList([ Argument(context.Left.Syntax.Left), Argument(context.Left.Syntax.Right), Argument(context.Right.Syntax.Right) ]));
			
			return true;
		}
		
		optimized = null;
		return false;
	}
}