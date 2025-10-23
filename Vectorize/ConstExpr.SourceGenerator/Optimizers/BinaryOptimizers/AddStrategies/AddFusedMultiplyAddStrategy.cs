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
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression }
		       && context.Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m =>
			       m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type)));
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		var host = ParseName(context.Type.Name);
		var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

		var multLeft = (BinaryExpressionSyntax) context.Left.Syntax;
		var aExpr = multLeft.Left;
		var bExpr = multLeft.Right;

		return InvocationExpression(fmaIdentifier,
			ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(context.Right.Syntax) ])));
	}
}