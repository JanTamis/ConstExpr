using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
///   Strategy for Fused Multiply-Add pattern: c - (a * b) => FMA(-a, b, c) (when FMA is available)
/// </summary>
public class SubtractFMARightMultiplyStrategy() : NumericBinaryStrategy<ExpressionSyntax, BinaryExpressionSyntax>(rightKind: SyntaxKind.MultiplyExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.FusedMultiplyAdd ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		var host = context.Type.AsTypeSyntax();
		var mulLeft = context.Right.Syntax.Left;
		var mulRight = context.Right.Syntax.Right;
		ExpressionSyntax negatedLeft, negatedRight;

		if (context.TryGetValue(mulRight, out var rightValue) && TryCreateLiteral(rightValue.Negate(), out var negatedRightLiteral))
		{
			negatedLeft = mulLeft;
			negatedRight = negatedRightLiteral;
		}
		else if (context.TryGetValue(mulLeft, out var leftValue) && TryCreateLiteral(leftValue.Negate(), out var negatedLeftLiteral))
		{
			negatedLeft = negatedLeftLiteral;
			negatedRight = mulRight;
		}
		else
		{
			negatedLeft = UnaryMinusExpression(mulLeft);
			negatedRight = mulRight;
		}

		var arguments = ArgumentList(negatedLeft, negatedRight, context.Left.Syntax);

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