using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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

		// x / (power of two) => x >> n (integer)
		if (Type.IsInteger() && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.RightShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			return true;
		}

		// x / (power of two) => x * (1/power) (floating point, FastMath)
		if (FloatingPointMode == FloatingPointEvaluationMode.FastMath
		    && !Type.IsInteger()
		    && hasRightValue
		    && !rightValue.IsNumericZero()
		    && ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Divide, 1.ToSpecialType(Type.SpecialType), rightValue.ToSpecialType(Type.SpecialType)) is { } reciprocal)
		{
			result = BinaryExpression(SyntaxKind.MultiplyExpression, Left, SyntaxHelpers.CreateLiteral(reciprocal)!);
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