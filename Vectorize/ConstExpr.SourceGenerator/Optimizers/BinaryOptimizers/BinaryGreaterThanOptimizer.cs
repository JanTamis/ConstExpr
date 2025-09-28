using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThan;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x > -1 = false (when x is unsigned) [symmetry with x < 0 = false]
			if (Type.IsUnsignedInteger() && hasRightValue && rightValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// x > 0 = true (when x is positive and non-zero and unsigned)
			if (hasRightValue && rightValue.IsNumericZero() && hasLeftValue && !leftValue.IsNumericZero())
			{
				if (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, leftValue, 0.ToSpecialType(Type.SpecialType)) is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// -1 > x = false for signed integer types
			if (!Type.IsUnsignedInteger() && hasLeftValue && leftValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}
		}

		// x > 0 = T.IsPositive(x)
		if (hasRightValue && rightValue.IsNumericZero() && LeftType?.HasMember<IMethodSymbol>("IsPositive", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsPositive")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(Left))));

			return true;
		}

		// 0 > x = T.IsNegative(x)
		if (hasLeftValue && leftValue.IsNumericZero() && RightType?.HasMember<IMethodSymbol>("IsNegative", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, RightType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(RightType.Name),
					IdentifierName("IsNegative")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(Right))));

			return true;
		}

		return false;
	}
}

