using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for Fused Multiply-Add (FMA) optimization:
/// (a * b) + c => FMA(a, b, c)
/// c + (a * b) => FMA(a, b, c)
/// </summary>
public class AddFusedMultiplyAddStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		var left = GetExpression(context.Left.Syntax);
		
		return left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression }
		       && (ContainsMultiplyAddEstimate(context.Type) || ContainsFusedMultiplyAdd(context.Type));
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		var host = ParseName(context.Type.Name);

		var multLeft = context.Left.Syntax is ParenthesizedExpressionSyntax parenExpr
			? (BinaryExpressionSyntax)parenExpr.Expression
			: (BinaryExpressionSyntax)context.Left.Syntax;
		
		var aExpr = multLeft.Left;
		var bExpr = multLeft.Right;

		if (context.Right.Value.IsNumericZero())
		{
			return multLeft;
		}
		
		var arguments = ArgumentList(SeparatedList([ Argument(GetExpression(aExpr)), Argument(GetExpression(bExpr)), Argument(GetExpression(context.Right.Syntax)) ]));

		if (ContainsMultiplyAddEstimate(context.Type))
		{
			return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("MultiplyAddEstimate")),
				arguments);
		}

		if (ContainsFusedMultiplyAdd(context.Type))
		{
			return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd")),
				arguments);
		}

		return null;
	}

	private bool ContainsMultiplyAddEstimate(ITypeSymbol type)
	{
		return type.HasMethod("MultiplyAddEstimate", m =>
			m.Parameters.Length == 3 &&
			m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, type)));
	}

	private bool ContainsFusedMultiplyAdd(ITypeSymbol type)
	{
		return type.HasMethod("FusedMultiplyAdd", m =>
			m.Parameters.Length == 3 &&
			m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, type)));
	}

	private ExpressionSyntax GetExpression(ExpressionSyntax syntax)
	{
		if (syntax is ParenthesizedExpressionSyntax parenthesizedExpression)
		{
			return parenthesizedExpression.Expression;
		}
		
		return syntax;
	}
}