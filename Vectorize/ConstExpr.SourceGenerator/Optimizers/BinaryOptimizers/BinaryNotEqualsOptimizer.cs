using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryNotEqualsOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.NotEquals;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x != x = false (for pure expressions)
		if (Left.IsEquivalentTo(Right) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// For boolean types: x != true => !x, x != false => x
		if (LeftType?.IsBoolType() == true && RightType?.IsBoolType() == true)
		{
			if (hasRightValue && rightValue is bool rbn)
			{
				result = rbn
					? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Left)) // x != true => !x
					: Left; // x != false => x
				return true;
			}

			if (hasLeftValue && leftValue is bool lbn)
			{
				result = lbn
					? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Right)) // true != x => !x
					: Right; // false != x => x
				return true;
			}
		}

		// (x % 2) != 0 => T.IsOddInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericZero() 
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } modExpr
		    && modExpr.Right.TryGetLiteralValue(loader, variables, out var modValue)
		    && modValue.IsNumericValue(2)
		    && LeftType?.IsInteger() == true
		    && LeftType.HasMember<IMethodSymbol>("IsOddInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))))
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsOddInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(modExpr.Left))));
			return true;
		}

		// (x % 2) != 1 => T.IsEvenInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericOne()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } modExpr2
		    && modExpr2.Right.TryGetLiteralValue(loader, variables, out var modValue2)
		    && modValue2.IsNumericValue(2)
		    && LeftType?.IsInteger() == true
		    && LeftType.HasMember<IMethodSymbol>("IsEvenInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))))
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsEvenInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(modExpr2.Left))));
			return true;
		}

		// (x & 1) != 0 => T.IsOddInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericZero()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr
		    && andExpr.Right.TryGetLiteralValue(loader, variables, out var andValue)
		    && andValue.IsNumericOne()
		    && LeftType?.IsInteger() == true
		    && LeftType.HasMember<IMethodSymbol>("IsOddInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))))
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsOddInteger")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(andExpr.Left))));
			return true;
		}

		// (x & 1) != 1 => T.IsEvenInteger(x) for integer types
		if (hasRightValue && rightValue.IsNumericOne()
		    && Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andExpr2
		    && andExpr2.Right.TryGetLiteralValue(loader, variables, out var andValue2)
		    && andValue2.IsNumericOne()
		    && LeftType?.IsInteger() == true
		    && LeftType.HasMember<IMethodSymbol>("IsOddInteger", m => m.Parameters.Length == 1 && m.Parameters.All(p => SymbolEqualityComparer.Default.Equals(p.Type, LeftType))))
		{
			result = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseTypeName(LeftType.Name),
					IdentifierName("IsEvenInteger")))
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
