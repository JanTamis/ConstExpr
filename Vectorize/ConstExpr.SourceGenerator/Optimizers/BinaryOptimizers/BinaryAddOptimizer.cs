using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAddOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Add;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

		Left.TryGetLiteralValue(loader, variables, out var leftValue);
		Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x + 0 = x (with strict handling of -0.0 unless FastMath)
		if (rightValue.IsNumericZero())
		{
			result = Left;
			return true;
		}

		// 0 + x = x
		if (leftValue.IsNumericZero())
		{
			result = Right;
			return true;
		}

		// x + (-x) => 0 and (-x) + x => 0 (pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rNeg
		    && rNeg.Operand.IsEquivalentTo(Left)
		    && IsPure(Left) && IsPure(rNeg.Operand))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } lNeg
		    && lNeg.Operand.IsEquivalentTo(Right)
		    && IsPure(Right) && IsPure(lNeg.Operand))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x + x => x << 1 (integer, pure)
		if (LeftEqualsRight(variables) && IsPure(Left) && IsPure(Right))
		{
			// x + x => x << 1 (integer, pure)
			if (Type.IsInteger())
			{
				result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left, SyntaxHelpers.CreateLiteral(1)!);
			}

			return true;
		}

		// x + -y  => x - y  (pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } unary
		    && IsPure(Left) && IsPure(unary.Operand))
		{
			var rightWithoutMinus = unary.Operand;
			result = BinaryExpression(SyntaxKind.SubtractExpression, Left, rightWithoutMinus);
			return true;
		}

		// -x + y  => y - x  (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } unary2
		    && IsPure(Right) && IsPure(unary2.Operand))
		{
			var leftWithoutMinus = unary2.Operand;
			result = BinaryExpression(SyntaxKind.SubtractExpression, Right, leftWithoutMinus);
			return true;
		}

		// -x + -y  => -(x + y)  (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } unary3
		    && Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } unary4
		    && IsPure(unary3.Operand) && IsPure(unary4.Operand))
		{
			var leftWithoutMinus = unary3.Operand;
			var rightWithoutMinus = unary4.Operand;

			var addition = ParenthesizedExpression(
				BinaryExpression(SyntaxKind.AddExpression, leftWithoutMinus, rightWithoutMinus));

			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, addition);
			return true;
		}

		// (x - a) + a => x (algebraic identity, pure)
		if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression } leftSub
		    && leftSub.Right.IsEquivalentTo(Right)
		    && IsPure(leftSub.Left) && IsPure(leftSub.Right) && IsPure(Right))
		{
			result = leftSub.Left;
			return true;
		}

		// a + (x - a) => x (algebraic identity, pure)
		if (Right is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression } rightSub
		    && rightSub.Right.IsEquivalentTo(Left)
		    && IsPure(Left) && IsPure(rightSub.Left) && IsPure(rightSub.Right))
		{
			result = rightSub.Left;
			return true;
		}

		// Fused Multiply-Add: (a * b) + c  OR  c + (a * b) => FMA(a,b,c)
		if (Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, Type))))
		{
			// var isFloat = Type.SpecialType == SpecialType.System_Single;
			var host = ParseName(Type.Name);
			var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

			// Pattern 1: (a * b) + c  (evaluation order preserved: a, b, c)
			if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multLeft)
			{
				var aExpr = multLeft.Left;
				var bExpr = multLeft.Right;

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(Right) ])));

				return true;
			}

			// Pattern 2: c + (a * b) (evaluation order changes; require purity for all three)
			if (Right is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multRight)
			{
				var aExpr = multRight.Left;
				var bExpr = multRight.Right;

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(Left) ])));

				return true;
			}
		}

		return false;
	}
}