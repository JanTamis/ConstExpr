using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for bitwise AND even detection: (x & 1) == 0 => T.IsEvenInteger(x)
/// </summary>
public class EqualsBitwiseAndEvenStrategy() 
	: SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseAndExpression)
{
	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;
		return expression;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var leftUnwrapped = UnwrapParentheses(context.Left.Syntax);
		var rightUnwrapped = UnwrapParentheses(context.Right.Syntax);

		if (leftUnwrapped == context.Left.Syntax && rightUnwrapped == context.Right.Syntax)
			return base.TryOptimize(context, out optimized);

		var unwrappedContext = new BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax>
		{
			Left = new BinaryOptimizeElement<ExpressionSyntax> { Syntax = leftUnwrapped, Type = context.Left.Type },
			Right = new BinaryOptimizeElement<ExpressionSyntax> { Syntax = rightUnwrapped, Type = context.Right.Type },
			Type = context.Type,
			Variables = context.Variables,
			TryGetValue = context.TryGetValue,
			BinaryExpressions = context.BinaryExpressions,
			Parent = context.Parent,
		};
		return base.TryOptimize(unwrappedContext, out optimized);
	}

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericZero()
		    || !context.Left.Syntax.Right.IsNumericOne()
		    || context.Left.Type?.HasMember<IMethodSymbol>(
			    "IsEvenInteger",
			    m => m.Parameters.Length == 1
			         && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Left.Type))) != true)
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(context.Left.Type!.Name),
					IdentifierName("IsEvenInteger")))
			.WithArgumentList(
				ArgumentList(
					SingletonSeparatedList(
						Argument(context.Left.Syntax.Left))));

		return true;
	}
}