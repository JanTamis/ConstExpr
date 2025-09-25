using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryDivideOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Divide;

	public override bool TryOptimize(bool hasLeftValue, object? leftValue, bool hasRightValue, object? rightValue, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

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

		// x / (power of two) => x >> n (integer)
		if (Type.IsInteger() && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.RightShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			return true;
		}

		// 1 / x = reciprocal
		if (FloatingPointMode == FloatingPointEvaluationMode.FastMath
		    && leftValue.IsNumericOne()
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