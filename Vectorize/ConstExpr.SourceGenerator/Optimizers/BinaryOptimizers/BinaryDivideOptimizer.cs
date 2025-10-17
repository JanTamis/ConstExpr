using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryDivideOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Divide;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x / 1 = x
		if (rightValue.IsNumericOne())
		{
			result = Left;
			return true;
		}

		// x / -1 = -x
		if (rightValue.IsNumericNegativeOne())
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Left);
			return true;
		}

		// 0 / x = 0 (when x != 0, integers only)
		if (Type.IsInteger() && leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x / x = 1 (pure, when x != 0 for integers, or FastMath for floats)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(1.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x / 2 => x >> 1 (unsigned integer only, signed division is different)
		if (Type.IsUnsignedInteger() && rightValue.IsNumericValue(2))
		{
			result = BinaryExpression(SyntaxKind.RightShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			return true;
		}

		// x / (power of two) => x >> n (unsigned integer)
		if (Type.IsUnsignedInteger() && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.RightShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			return true;
		}

		// x / (power of two) => x * (1/power) (floating point)
		if (!Type.IsInteger()
		    && hasRightValue
		    && !rightValue.IsNumericZero()
		    && ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Divide, 1.ToSpecialType(Type.SpecialType), rightValue.ToSpecialType(Type.SpecialType)) is { } reciprocal)
		{
			result = BinaryExpression(SyntaxKind.MultiplyExpression, Left, SyntaxHelpers.CreateLiteral(reciprocal)!);
			return true;
		}

		// (-x) / (-y) => x / y (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftNeg
		    && Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightNeg
		    && IsPure(leftNeg.Operand) && IsPure(rightNeg.Operand))
		{
			result = BinaryExpression(SyntaxKind.DivideExpression, leftNeg.Operand, rightNeg.Operand);
			return true;
		}

		// (-x) / y => -(x / y) (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftNeg2
		    && IsPure(leftNeg2.Operand) && IsPure(Right))
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.DivideExpression, leftNeg2.Operand, Right)));
			return true;
		}

		// x / (-y) => -(x / y) (pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightNeg2
		    && IsPure(Left) && IsPure(rightNeg2.Operand))
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.DivideExpression, Left, rightNeg2.Operand)));
			return true;
		}

		// 1 / x = reciprocal
		if (leftValue.IsNumericOne()
		    && Type.HasMember<IMethodSymbol>("ReciprocalEstimate", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, Type))))
		{
			var host = ParseName(Type.Name);
			var reciprocalIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("ReciprocalEstimate"));

			result = InvocationExpression(reciprocalIdentifier, ArgumentList(SingletonSeparatedList(Argument(Right))));

			return true;
		}

		return false;
	}
}