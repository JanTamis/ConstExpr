using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinarySubtractOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Subtract;

	public override bool TryOptimize(bool hasLeftValue, object? leftValue, bool hasRightValue, object? rightValue, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

		// x - 0 = x
		if (hasRightValue && rightValue.IsNumericZero())
		{
			result = Left;
			return true;
		}

		// 0 - x = -x
		if (hasLeftValue && leftValue.IsNumericZero())
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Right);
			return true;
		}

		// x - x = 0 (pure)
		if (Left.IsEquivalentTo(Right) && IsPure(Operation.LeftOperand) && IsPure(Operation.RightOperand)
		    && (Type.IsInteger() || FloatingPointMode == FloatingPointEvaluationMode.FastMath))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x - -y  => x + y (pure)
		if (Operation.RightOperand is IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary
		    && IsPure(Operation.LeftOperand) && IsPure(unary.Operand))
		{
			result = BinaryExpression(SyntaxKind.AddExpression, Left, Right);
			return true;
		}

		// Fused Multiply-Add pattern: (a * b) - c => FMA(a,b,-c)
		// Only in FastMath and for float/double (semantic change due to single rounding)
		if (FloatingPointMode == FloatingPointEvaluationMode.FastMath
		    && Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, Type))))
		{
			var host = ParseName(Type.Name);
			var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

			if (Operation.LeftOperand is IBinaryOperation { OperatorKind: BinaryOperatorKind.Multiply } multLeft
			    && IsPure(multLeft.LeftOperand) && IsPure(multLeft.RightOperand) && IsPure(Operation.RightOperand))
			{
				var aExpr = (ExpressionSyntax) multLeft.LeftOperand.Syntax;
				var bExpr = (ExpressionSyntax) multLeft.RightOperand.Syntax;
				var cExpr = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Right);

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(cExpr) ])));
				return true;
			}
		}

		return false;
	}
}