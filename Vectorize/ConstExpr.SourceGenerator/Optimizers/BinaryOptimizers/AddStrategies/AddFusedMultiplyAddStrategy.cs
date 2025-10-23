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
public class AddFusedMultiplyAddStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return (CanOptimizeLeftMultiplication(context)
			|| CanOptimizeRightMultiplication(context))
			&& context.Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type)));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var host = ParseName(context.Type.Name);
		var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

		// Pattern 1: (a * b) + c  (evaluation order preserved: a, b, c)
		if (CanOptimizeLeftMultiplication(context))
		{
			var multLeft = (BinaryExpressionSyntax)context.Left.Syntax;
			var aExpr = multLeft.Left;
			var bExpr = multLeft.Right;

			return InvocationExpression(fmaIdentifier,
				ArgumentList(SeparatedList([Argument(aExpr), Argument(bExpr), Argument(context.Right.Syntax)])));
		}

		// Pattern 2: c + (a * b) (evaluation order changes; require purity for all three)
		if (CanOptimizeRightMultiplication(context))
		{
			var multRight = (BinaryExpressionSyntax)context.Right.Syntax;
			var aExpr = multRight.Left;
			var bExpr = multRight.Right;

			return InvocationExpression(fmaIdentifier,
				ArgumentList(SeparatedList([Argument(aExpr), Argument(bExpr), Argument(context.Left.Syntax)])));
		}

		return null;
	}

	private static bool CanOptimizeLeftMultiplication(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression };
	}

	private static bool CanOptimizeRightMultiplication(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression };
	}
}
