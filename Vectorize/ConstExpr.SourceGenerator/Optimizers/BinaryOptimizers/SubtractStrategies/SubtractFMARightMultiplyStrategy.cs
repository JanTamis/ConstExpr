using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for Fused Multiply-Add pattern: c - (a * b) => FMA(-a, b, c) (when FMA is available)
/// </summary>
public class SubtractFMARightMultiplyStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && (ContainsMultiplyAddEstimate(context.Type) || ContainsFusedMultiplyAdd(context.Type))
		       && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } multRight
		       && IsPure(multRight.Left)
		       && IsPure(multRight.Right)
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var multRight = (BinaryExpressionSyntax)context.Right.Syntax;
		var host = ParseName(context.Type.Name);
		
		var aExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, multRight.Left);
		var bExpr = multRight.Right;

		var arguments = ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(context.Left.Syntax) ]));

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
