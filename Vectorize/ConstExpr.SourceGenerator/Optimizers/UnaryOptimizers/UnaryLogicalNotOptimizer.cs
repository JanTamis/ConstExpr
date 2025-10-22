using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.UnaryOptimizers;

public class UnaryLogicalNotOptimizer
{
	public ExpressionSyntax Operand { get; init; }
	public ITypeSymbol Type { get; init; }

	public bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		// !!x => x (double negation)
		if (Operand is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } innerNot)
		{
			result = innerNot.Operand;
			return true;
		}

		// De Morgan's Law: !(a && b) => !a || !b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } andExpr)
		{
			var notLeft = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(andExpr.Left));
			var notRight = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(andExpr.Right));
			result = BinaryExpression(SyntaxKind.LogicalOrExpression, notLeft, notRight);
			return true;
		}

		// De Morgan's Law: !(a || b) => !a && !b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } orExpr)
		{
			var notLeft = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(orExpr.Left));
			var notRight = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(orExpr.Right));
			result = BinaryExpression(SyntaxKind.LogicalAndExpression, notLeft, notRight);
			return true;
		}

		// !(a < b) => a >= b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } ltExpr)
		{
			result = BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, ltExpr.Left, ltExpr.Right);
			return true;
		}

		// !(a > b) => a <= b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanExpression } gtExpr)
		{
			result = BinaryExpression(SyntaxKind.LessThanOrEqualExpression, gtExpr.Left, gtExpr.Right);
			return true;
		}

		// !(a <= b) => a > b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanOrEqualExpression } leExpr)
		{
			result = BinaryExpression(SyntaxKind.GreaterThanExpression, leExpr.Left, leExpr.Right);
			return true;
		}

		// !(a >= b) => a < b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanOrEqualExpression } geExpr)
		{
			result = BinaryExpression(SyntaxKind.LessThanExpression, geExpr.Left, geExpr.Right);
			return true;
		}

		// !(a == b) => a != b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } eqExpr)
		{
			result = BinaryExpression(SyntaxKind.NotEqualsExpression, eqExpr.Left, eqExpr.Right);
			return true;
		}

		// !(a != b) => a == b
		if (Operand is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.NotEqualsExpression } neExpr)
		{
			result = BinaryExpression(SyntaxKind.EqualsExpression, neExpr.Left, neExpr.Right);
			return true;
		}

		return false;
	}
}

