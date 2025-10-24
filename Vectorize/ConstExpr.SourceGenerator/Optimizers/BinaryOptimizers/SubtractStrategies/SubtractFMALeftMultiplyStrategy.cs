using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for Fused Multiply-Add pattern: (a * b) - c => FMA(a,b,-c) (when FMA is available)
/// </summary>
public class SubtractFMALeftMultiplyStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && (ContainsMultiplyAddEstimate(context.Type) || ContainsFusedMultiplyAdd(context.Type))
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } multLeft
		       && IsPure(multLeft.Left)
		       && IsPure(multLeft.Right)
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var multLeft = (BinaryExpressionSyntax)context.Left.Syntax;
		var host = ParseName(context.Type.Name);
		
		var aExpr = multLeft.Left;
		var bExpr = multLeft.Right;
		var cExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Right.Syntax);

		var arguments = ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(cExpr) ]));

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
}
