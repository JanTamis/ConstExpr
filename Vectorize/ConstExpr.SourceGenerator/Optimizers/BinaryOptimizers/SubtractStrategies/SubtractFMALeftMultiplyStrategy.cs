using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for Fused Multiply-Add pattern: (a * b) - c => FMA(a,b,-c) (when FMA is available)
/// Requires FusedMultiplyAdd flag as FMA has different rounding behavior.
/// </summary>
public class SubtractFMALeftMultiplyStrategy() : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.MultiplyExpression)
{
	public override FastMathFlags RequiredFlags => FastMathFlags.FusedMultiplyAdd;

	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		var host = ParseName(context.Type.Name);

		var negatedRightOperand = context.Right.Syntax is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax
			? UnaryMinusExpression(ParenthesizedExpression(context.Right.Syntax))
			: UnaryMinusExpression(context.Right.Syntax);

		var arguments = ArgumentList(SeparatedList(
		[
			Argument(context.Left.Syntax.Left),
			Argument(context.Left.Syntax.Right),
			Argument(negatedRightOperand)
		]));

		if (ContainsMultiplyAddEstimate(context.Type))
		{
			optimized = InvocationExpression(MemberAccessExpression(host, IdentifierName("MultiplyAddEstimate")), arguments);
			return true;
		}

		if (ContainsFusedMultiplyAdd(context.Type))
		{
			optimized = InvocationExpression(MemberAccessExpression(host, IdentifierName("FusedMultiplyAdd")), arguments);
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