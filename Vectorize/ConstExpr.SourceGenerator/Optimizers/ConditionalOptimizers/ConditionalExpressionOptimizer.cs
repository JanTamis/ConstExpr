using System.Collections.Generic;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.ConditionalOptimizers;

public class ConditionalExpressionOptimizer
{
	public ExpressionSyntax Condition { get; init; }
	public ExpressionSyntax WhenTrue { get; init; }
	public ExpressionSyntax WhenFalse { get; init; }
	public ITypeSymbol? Type { get; init; }

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
			result = LogicalNotExpression(ParenthesizedExpression(Condition));
			return true;
		}

		// condition ? x : x => x (when x is pure)
		if (WhenTrue.EqualsTo(WhenFalse))
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
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } ltExpr
		    && ltExpr.Left.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && ltExpr.Right.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MinNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		// a > b ? a : b => Math.Max(a, b)
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanExpression } gtExpr
		    && gtExpr.Left.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && gtExpr.Right.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MaxNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		// a <= b ? a : b => Math.Min(a, b)
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanOrEqualExpression } leExpr
		    && leExpr.Left.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && leExpr.Right.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MinNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		// a >= b ? a : b => Math.Max(a, b)
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanOrEqualExpression } geExpr
		    && geExpr.Left.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && geExpr.Right.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MaxNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		// b < a ? a : b => Math.Max(a, b)
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LessThanExpression } ltExpr2
		    && ltExpr2.Right.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && ltExpr2.Left.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MaxNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		// b > a ? a : b => Math.Min(a, b)
		if (Type?.IsNumericType() == true
		    && Condition is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.GreaterThanExpression } gtExpr2
		    && gtExpr2.Right.GetDeterministicHash() == WhenTrue.GetDeterministicHash()
		    && gtExpr2.Left.GetDeterministicHash() == WhenFalse.GetDeterministicHash()
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			var mathType = ParseTypeName(Type.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("MinNative")))
				.WithArgumentList(ArgumentList(SeparatedList([ Argument(WhenTrue), Argument(WhenFalse) ])));
			return true;
		}

		if (TryGetNullConditionalCoalescePattern(Condition, WhenTrue, WhenFalse, out var receiver, out var memberName, out var fallback))
		{
			// (x == null ? fallback : x.Member) and equivalent forms => x?.Member ?? fallback
			result = BinaryExpression(SyntaxKind.CoalesceExpression,
				ConditionalAccessExpression(receiver, MemberBindingExpression(memberName)),
				fallback);
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

	private static bool TryGetNullConditionalCoalescePattern(
		ExpressionSyntax condition,
		ExpressionSyntax whenTrue,
		ExpressionSyntax whenFalse,
		out ExpressionSyntax receiver,
		out SimpleNameSyntax memberName,
		out ExpressionSyntax fallback)
	{
		receiver = null!;
		memberName = null!;
		fallback = null!;

		if (!TryGetNullCheck(condition, out var checkedExpression, out var isNullWhenConditionIsTrue))
		{
			return false;
		}

		var memberBranch = isNullWhenConditionIsTrue ? whenFalse : whenTrue;
		var fallbackBranch = isNullWhenConditionIsTrue ? whenTrue : whenFalse;

		if (memberBranch is not MemberAccessExpressionSyntax memberAccess
		    || !SyntaxNodeComparer.Get().Equals(memberAccess.Expression, checkedExpression))
		{
			return false;
		}

		receiver = checkedExpression;
		memberName = memberAccess.Name;
		fallback = fallbackBranch;
		return true;
	}

	private static bool TryGetNullCheck(ExpressionSyntax condition, out ExpressionSyntax expression, out bool isNullWhenConditionIsTrue)
	{
		expression = null!;
		isNullWhenConditionIsTrue = false;

		var unwrappedCondition = UnwrapParentheses(condition);

		if (unwrappedCondition is BinaryExpressionSyntax binary)
		{
			var kind = (SyntaxKind) binary.RawKind;

			if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
			{
				if (IsNullLiteral(binary.Right))
				{
					expression = UnwrapParentheses(binary.Left);
					isNullWhenConditionIsTrue = kind == SyntaxKind.EqualsExpression;
					return true;
				}

				if (IsNullLiteral(binary.Left))
				{
					expression = UnwrapParentheses(binary.Right);
					isNullWhenConditionIsTrue = kind == SyntaxKind.EqualsExpression;
					return true;
				}
			}
		}

		if (unwrappedCondition is IsPatternExpressionSyntax isPattern)
		{
			var unwrappedPattern = UnwrapParenthesizedPattern(isPattern.Pattern);

			if (unwrappedPattern is ConstantPatternSyntax { Expression: var constantExpression } && IsNullLiteral(constantExpression))
			{
				expression = UnwrapParentheses(isPattern.Expression);
				isNullWhenConditionIsTrue = true;
				return true;
			}

			if (unwrappedPattern is UnaryPatternSyntax
			    {
				    RawKind: (int) SyntaxKind.NotPattern,
				    Pattern: var notPattern
			    })
			{
				var nestedPattern = UnwrapParenthesizedPattern(notPattern);

				if (nestedPattern is ConstantPatternSyntax { Expression: var nestedConstantExpression } && IsNullLiteral(nestedConstantExpression))
				{
					expression = UnwrapParentheses(isPattern.Expression);
					isNullWhenConditionIsTrue = false;
					return true;
				}
			}
		}

		return false;
	}

	private static bool IsNullLiteral(ExpressionSyntax expression)
	{
		return UnwrapParentheses(expression) is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression };
	}

	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
		{
			expression = parenthesized.Expression;
		}

		return expression;
	}

	private static PatternSyntax UnwrapParenthesizedPattern(PatternSyntax pattern)
	{
		while (pattern is ParenthesizedPatternSyntax parenthesizedPattern)
		{
			pattern = parenthesizedPattern.Pattern;
		}

		return pattern;
	}
}