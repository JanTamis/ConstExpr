using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.ConditionalOptimizers;

public class ConditionalExpressionOptimizer
{
	public ExpressionSyntax Condition { get; init; }
	public ExpressionSyntax WhenTrue { get; init; }
	public ExpressionSyntax WhenFalse { get; init; }
	public ITypeSymbol Type { get; init; }

	public bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		// condition ? true : false => condition (when condition is bool)
		if (WhenTrue.TryGetLiteralValue(loader, variables, out var trueValue) && trueValue is true
		    && WhenFalse.TryGetLiteralValue(loader, variables, out var falseValue) && falseValue is false)
		{
			result = Condition;
			return true;
		}

		// condition ? false : true => !condition
		if (WhenTrue.TryGetLiteralValue(loader, variables, out var trueValue2) && trueValue2 is false
		    && WhenFalse.TryGetLiteralValue(loader, variables, out var falseValue2) && falseValue2 is true)
		{
			result = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Condition));
			return true;
		}

		// condition ? x : x => x (when x is pure)
		if (WhenTrue.IsEquivalentTo(WhenFalse) && IsPure(WhenTrue))
		{
			result = WhenTrue;
			return true;
		}

		// true ? a : b => a
		if (Condition.TryGetLiteralValue(loader, variables, out var condValue) && condValue is true)
		{
			result = WhenTrue;
			return true;
		}

		// false ? a : b => b
		if (Condition.TryGetLiteralValue(loader, variables, out var condValue2) && condValue2 is false)
		{
			result = WhenFalse;
			return true;
		}

		// a < b ? a : b => Math.Min(a, b) (for numeric types)
		if (Type.IsNumericType() 
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } ltExpr
		    && ltExpr.Left.IsEquivalentTo(WhenTrue)
		    && ltExpr.Right.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Min")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		// a > b ? a : b => Math.Max(a, b)
		if (Type.IsNumericType()
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanExpression } gtExpr
		    && gtExpr.Left.IsEquivalentTo(WhenTrue)
		    && gtExpr.Right.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Max")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		// a <= b ? a : b => Math.Min(a, b)
		if (Type.IsNumericType()
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanOrEqualExpression } leExpr
		    && leExpr.Left.IsEquivalentTo(WhenTrue)
		    && leExpr.Right.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Min")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		// a >= b ? a : b => Math.Max(a, b)
		if (Type.IsNumericType()
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanOrEqualExpression } geExpr
		    && geExpr.Left.IsEquivalentTo(WhenTrue)
		    && geExpr.Right.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Max")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		// b < a ? a : b => Math.Max(a, b)
		if (Type.IsNumericType()
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } ltExpr2
		    && ltExpr2.Right.IsEquivalentTo(WhenTrue)
		    && ltExpr2.Left.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Max")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		// b > a ? a : b => Math.Min(a, b)
		if (Type.IsNumericType()
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanExpression } gtExpr2
		    && gtExpr2.Right.IsEquivalentTo(WhenTrue)
		    && gtExpr2.Left.IsEquivalentTo(WhenFalse)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mathType, IdentifierName("Min")))
				.WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(WhenTrue), Argument(WhenFalse) })));
			return true;
		}

		return false;
	}

	private static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}
}

