using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

public class EqualsComparisonSimplifierStrategy
	: SymmetricStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var method = TryGetOppositeOperator(context.Left.Syntax.Kind());

		if (method is null)
		{
			optimized = null;
			return false;
		}
		
		if (context.Left.Syntax.Left is LiteralExpressionSyntax leftLiteral)
		{
			var result = method(context.Right.Syntax.Token.Value, leftLiteral.Token.Value);

			if (result != null
			    && SyntaxHelpers.TryGetLiteral(result, out var optimizedLiteral))
			{
				optimized = SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, context.Left.Syntax.Right, optimizedLiteral);
				return true;
			}
		}

		if (context.Left.Syntax.Right is LiteralExpressionSyntax rightLiteral)
		{
			var result = method(context.Right.Syntax.Token.Value, rightLiteral.Token.Value);

			if (result != null
			    && SyntaxHelpers.TryGetLiteral(result, out var optimizedLiteral))
			{
				optimized = SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, context.Left.Syntax.Left, optimizedLiteral);
				return true;
			}
		}
		
		optimized = null;
		return false;
	}
	
	private static Func<object?, object?, object?>? TryGetOppositeOperator(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.AddExpression => ObjectExtensions.Subtract,
			SyntaxKind.SubtractExpression => ObjectExtensions.Add,
			SyntaxKind.MultiplyExpression => ObjectExtensions.Divide,
			SyntaxKind.DivideExpression => ObjectExtensions.Multiply,
			SyntaxKind.LeftShiftExpression => ObjectExtensions.RightShift,
			SyntaxKind.RightShiftExpression => ObjectExtensions.LeftShift,
			_ => null
		};
	}
}