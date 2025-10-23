using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryEqualsOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Equals;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x == x = true (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// For boolean types: x == true => x, x == false => !x
		if (LeftType?.IsBoolType() == true && RightType?.IsBoolType() == true)
		{
			if (hasRightValue && rightValue is bool rb)
			{
				result = rb
					? Left // x == true => x
					: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Left)); // x == false => !x
				return true;
			}

			if (hasLeftValue && leftValue is bool lb)
			{
				result = lb
					? Right // true == x => x
					: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Right)); // false == x => !x
				return true;
			}
		}

		// Integer range-based optimizations
		if (LeftType?.IsInteger() == true || RightType?.IsInteger() == true)
		{
			var intType = LeftType?.IsInteger() == true ? LeftType : RightType;

			// unsigned == negative_constant => false
			if (intType?.IsUnsignedInteger() == true && hasRightValue)
			{
				var isNegative = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, rightValue, 0.ToSpecialType(intType.SpecialType)) is true;
				if (isNegative)
				{
					result = SyntaxHelpers.CreateLiteral(false);
					return true;
				}
			}

			// unsigned == negative_constant => false (reversed)
			if (intType?.IsUnsignedInteger() == true && hasLeftValue)
			{
				var isNegative = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, leftValue, 0.ToSpecialType(intType.SpecialType)) is true;
				if (isNegative)
				{
					result = SyntaxHelpers.CreateLiteral(false);
					return true;
				}
			}
		}

		// (x % 2) == 0 => T.IsEvenInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericZero() 
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } modExpr
		    && modExpr.Right.TryGetLiteralValue(loader, variables, out var modValue)
		    && modValue.IsNumericValue(2)
		    // && LeftType?.IsInteger() == true
		    && LeftType?.HasMember<IMethodSymbol>("IsEvenInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsEvenInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(modExpr.Left))));
			return true;
		}

		// (x % 2) == 1 => T.IsOddInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericOne()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } modExpr2
		    && modExpr2.Right.TryGetLiteralValue(loader, variables, out var modValue2)
		    && modValue2.IsNumericValue(2)
		    // && LeftType?.IsInteger() == true
		    && LeftType?.HasMember<IMethodSymbol>("IsOddInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsOddInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(modExpr2.Left))));
			return true;
		}

		// (x & 1) == 0 => T.IsEvenInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericZero()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr
		    && andExpr.Right.TryGetLiteralValue(loader, variables, out var andValue)
		    && andValue.IsNumericOne()
		    // && LeftType?.IsInteger() == true
		    && LeftType?.HasMember<IMethodSymbol>("IsEvenInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsEvenInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(andExpr.Left))));
			return true;
		}

		// (x & 1) == 1 => T.IsOddInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericOne()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr2
		    && andExpr2.Right.TryGetLiteralValue(loader, variables, out var andValue2)
		    && andValue2.IsNumericOne()
		    // && LeftType?.IsInteger() == true
		    && LeftType?.HasMember<IMethodSymbol>("IsOddInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))) == true)
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsOddInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(andExpr2.Left))));
			return true;
		}

		// Both sides are constant, evaluate
		if (hasLeftValue && hasRightValue)
		{
			var evalResult = ObjectExtensions.ExecuteBinaryOperation(Kind, leftValue, rightValue);
			
			if (evalResult != null)
			{
				result = SyntaxHelpers.CreateLiteral(evalResult);
				return true;
			}
		}

		return false;
	}
}
