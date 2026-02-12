using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for Fused Multiply-Add (FMA) optimization:
/// (a * b) + c => FMA(a, b, c)
/// c + (a * b) => FMA(a, b, c)
/// </summary>
public class AddFusedMultiplyAddStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.MultiplyExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var host = ParseName(context.Type.Name);

		var arguments = ArgumentList(SeparatedList([ Argument(context.Left.Syntax.Left), Argument(context.Left.Syntax.Right), Argument(context.Right.Syntax) ]));

		if (ContainsMultiplyAddEstimate(context.Type))
		{
			optimized = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
					host, 
					IdentifierName("MultiplyAddEstimate")),
				arguments);
			
			return true;
		}

		if (ContainsFusedMultiplyAdd(context.Type))
		{
			optimized = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
					host,
					IdentifierName("FusedMultiplyAdd")),
				arguments);
			return true;
		}

		optimized = null;
		return false;
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