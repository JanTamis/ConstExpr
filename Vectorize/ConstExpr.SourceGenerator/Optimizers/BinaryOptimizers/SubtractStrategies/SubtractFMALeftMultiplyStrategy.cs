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
		       && context.Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => 
			       m.Parameters.Length == 3 && 
			       m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, context.Type)))
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } multLeft
		       && IsPure(multLeft.Left)
		       && IsPure(multLeft.Right)
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var multLeft = (BinaryExpressionSyntax)context.Left.Syntax;
		var host = ParseName(context.Type.Name);
		var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));
		
		var aExpr = multLeft.Left;
		var bExpr = multLeft.Right;
		var cExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Right.Syntax);

		return InvocationExpression(fmaIdentifier,
			ArgumentList(SeparatedList([Argument(aExpr), Argument(bExpr), Argument(cExpr)])));
	}
}
