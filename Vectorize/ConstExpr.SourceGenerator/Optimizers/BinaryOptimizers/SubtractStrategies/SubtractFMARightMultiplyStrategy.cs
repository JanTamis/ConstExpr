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
		       && context.Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => 
			       m.Parameters.Length == 3 && 
			       m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type)))
		       && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } multRight
		       && IsPure(multRight.Left)
		       && IsPure(multRight.Right)
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var multRight = (BinaryExpressionSyntax)context.Right.Syntax;
		var host = ParseName(context.Type.Name);
		var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));
		
		var aExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, multRight.Left);
		var bExpr = multRight.Right;

		return InvocationExpression(fmaIdentifier,
			ArgumentList(SeparatedList([Argument(aExpr), Argument(bExpr), Argument(context.Left.Syntax)])));
	}
}
