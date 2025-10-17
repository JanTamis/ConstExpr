using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLessThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LessThan;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x < x => false (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x < 0 = false (when x is unsigned)
			if (Type.IsUnsignedInteger() && rightValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// 0 < x = true (when x is positive and non-zero and unsigned)
			if (leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero())
			{
				if (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, rightValue, 0.ToSpecialType(Type.SpecialType)) is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// x < -1 = false for signed integer types
			if (!Type.IsUnsignedInteger() && rightValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}
		}

		// x < 0 = T.IsNegative(x)
		if (rightValue.IsNumericZero() && RightType?.HasMember<IMethodSymbol>("IsNegative", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, RightType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(RightType.Name),
					IdentifierName("IsNegative")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(Left))));

			return true;
		}
		
		// 0 < x = T.IsPositive(x)
		if (leftValue.IsNumericZero() && LeftType?.HasMember<IMethodSymbol>("IsPositive", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsPositive")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(Right))));
			
			return true;
		}

		return false;
	}
}