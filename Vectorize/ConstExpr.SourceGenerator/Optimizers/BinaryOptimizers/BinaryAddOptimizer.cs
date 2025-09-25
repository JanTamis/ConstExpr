using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAddOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Add;

	public override bool TryOptimize(bool hasLeftValue, object? leftValue, bool hasRightValue, object? rightValue, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

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

		// x + x => x << 1 (integer, pure)
		if (Left.IsEquivalentTo(Right) && IsPure(Operation.LeftOperand) && IsPure(Operation.RightOperand))
		{
			// x + x => x << 1 (integer, pure)
			if (Type.IsInteger())
			{
				result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
					LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			}
			// x + x => x * 2 (pure)
			else
			{
				result = BinaryExpression(SyntaxKind.MultiplyExpression, Left, SyntaxHelpers.CreateLiteral(2.ToSpecialType(Type.SpecialType)));
			}
			
			return true;
		}
		
		// x + -y  => x - y  (pure)
		if (Operation.RightOperand is IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary
		    && IsPure(Operation.LeftOperand) && IsPure(unary.Operand))
		{
			var rightWithoutMinus = (ExpressionSyntax)unary.Operand.Syntax;
			result = BinaryExpression(SyntaxKind.SubtractExpression, Left, rightWithoutMinus);
			return true;
		}
		
		// -x + y  => y - x  (pure)
		if (Operation.LeftOperand is IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary2
		    && IsPure(Operation.RightOperand) && IsPure(unary2.Operand))
		{
			var leftWithoutMinus = (ExpressionSyntax)unary2.Operand.Syntax;
			result = BinaryExpression(SyntaxKind.SubtractExpression, Right, leftWithoutMinus);
			return true;
		}
		
		// -x + -y  => -(x + y)  (pure)
		if (Operation is { LeftOperand: IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary3, RightOperand: IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary4 }
		    && IsPure(unary3.Operand) && IsPure(unary4.Operand))
		{
			var leftWithoutMinus = (ExpressionSyntax)unary3.Operand.Syntax;
			var rightWithoutMinus = (ExpressionSyntax)unary4.Operand.Syntax;
			
			var addition = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, leftWithoutMinus, rightWithoutMinus));
			
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, addition);
			return true;
		}

		// Fused Multiply-Add: (a * b) + c  OR  c + (a * b) => FMA(a,b,c)
		// Only when FastMath (can change rounding semantics) and for float/double
		if (FloatingPointMode == FloatingPointEvaluationMode.FastMath 
		    && Type.HasMember<IMethodSymbol>("FusedMultiplyAdd", m => m.Parameters.Length == 3 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, Type))))
		{
			// var isFloat = Type.SpecialType == SpecialType.System_Single;
			var host = ParseName(Type.Name);
			var fmaIdentifier = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, host, IdentifierName("FusedMultiplyAdd"));

			// Pattern 1: (a * b) + c  (evaluation order preserved: a, b, c)
			if (Operation.LeftOperand is IBinaryOperation { OperatorKind: BinaryOperatorKind.Multiply } multLeft
			    && IsPure(multLeft.LeftOperand) && IsPure(multLeft.RightOperand) && IsPure(Operation.RightOperand))
			{
				var aExpr = (ExpressionSyntax)multLeft.LeftOperand.Syntax;
				var bExpr = (ExpressionSyntax)multLeft.RightOperand.Syntax;

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(Right) ])));
				
				return true;
			}

			// Pattern 2: c + (a * b) (evaluation order changes; require purity for all three)
			if (Operation.RightOperand is IBinaryOperation { OperatorKind: BinaryOperatorKind.Multiply } multRight
			    && IsPure(Operation.LeftOperand) && IsPure(multRight.LeftOperand) && IsPure(multRight.RightOperand))
			{
				var aExpr = (ExpressionSyntax)multRight.LeftOperand.Syntax;
				var bExpr = (ExpressionSyntax)multRight.RightOperand.Syntax;

				result = InvocationExpression(fmaIdentifier,
					ArgumentList(SeparatedList([ Argument(aExpr), Argument(bExpr), Argument(Left) ])));
				
				return true;
			}
		}

		return false;
	}
}