using System.Collections.Generic;
using System.Linq;
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
		if (WhenTrue.TryGetLiteralValue(loader, variables, out var trueValue)
		    && trueValue is true
		    && WhenFalse.TryGetLiteralValue(loader, variables, out var falseValue) && falseValue is false)
		{
			result = Condition;
			return true;
		}

		// condition ? false : true => !condition
		if (WhenTrue.TryGetLiteralValue(loader, variables, out var trueValue2)
		    && trueValue2 is false
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

		// condition ? x : false => condition && x (both branches short-circuit identically)
		if (WhenFalse.TryGetLiteralValue(loader, variables, out var whenFalseValue) && whenFalseValue is false)
		{
			result = BinaryExpression(SyntaxKind.LogicalAndExpression, Condition, WhenTrue);
			return true;
		}

		// condition ? true : x => condition || x (both branches short-circuit identically)
		if (WhenTrue.TryGetLiteralValue(loader, variables, out var whenTrueValue) && whenTrueValue is true)
		{
			result = BinaryExpression(SyntaxKind.LogicalOrExpression, Condition, WhenFalse);
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

		// value < min ? min : value > max ? max : value => T.ClampNative/Clamp(value, min, max)
		// NB: int.Clamp throws for min > max where the original chain returned min; like the
		// float ClampNative rewrite, we assume the bounds are ordered
		if ((Type?.IsFloatingNumeric() == true || Type?.IsInteger() == true)
		    && TryGetClampPattern(Condition, WhenTrue, WhenFalse, out var clampValue, out var clampMin, out var clampMax)
		    && IsPure(clampValue)
		    && IsPure(clampMin)
		    && IsPure(clampMax))
		{
			result = MathInvocation(Type.IsFloatingNumeric() ? "ClampNative" : "Clamp", clampValue, clampMin, clampMax);
			return true;
		}

		// a < b ? a : b => T.MinNative/Min(a, b), a > b ? a : b => T.MaxNative/Max(a, b), including mirrored arms
		if ((Type?.IsFloatingNumeric() == true || Type?.IsInteger() == true)
		    && TryGetMinMaxPattern(out var isMin)
		    && IsPure(WhenTrue) && IsPure(WhenFalse))
		{
			result = MathInvocation((Type.IsFloatingNumeric(), isMin) switch
			{
				(true, true) => "MinNative",
				(true, false) => "MaxNative",
				(false, true) => "Min",
				(false, false) => "Max"
			}, WhenTrue, WhenFalse);
			return true;
		}

		// Variants like: n < 0 ? -n : n, n > 0 ? n : -n, 0 > n ? -n : n => T.Abs(n)
		if (Type?.IsNumericType() == true
		    && TryGetAbsoluteValuePattern(Condition, WhenTrue, WhenFalse, out var absoluteValueInput))
		{
			var mathType = ParseTypeName(Type.Name);

			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("Abs")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(absoluteValueInput))));
			return true;
		}

		// (x == null ? fallback : x.Member) and equivalent forms => x?.Member ?? fallback
		if (TryGetNullConditionalCoalescePattern(Condition, WhenTrue, WhenFalse, out var receiver, out var memberName, out var fallback))
		{
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

	private InvocationExpressionSyntax MathInvocation(string name, params ExpressionSyntax[] arguments)
	{
		return InvocationExpression(MemberAccessExpression(ParseTypeName(Type!.Name), IdentifierName(name)))
			.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
	}

	private bool TryGetMinMaxPattern(out bool isMin)
	{
		isMin = false;

		if (UnwrapParentheses(Condition) is not BinaryExpressionSyntax binary)
		{
			return false;
		}

		var kind = (SyntaxKind) binary.RawKind;

		if (kind is not (SyntaxKind.LessThanExpression
		    or SyntaxKind.LessThanOrEqualExpression
		    or SyntaxKind.GreaterThanExpression
		    or SyntaxKind.GreaterThanOrEqualExpression))
		{
			return false;
		}

		var comparer = SyntaxNodeComparer.Get();
		var left = UnwrapParentheses(binary.Left);
		var right = UnwrapParentheses(binary.Right);
		var trueBranch = UnwrapParentheses(WhenTrue);
		var falseBranch = UnwrapParentheses(WhenFalse);
		var lessLike = kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression;

		// a < b ? a : b => Min, a > b ? a : b => Max
		if (comparer.Equals(left, trueBranch) && comparer.Equals(right, falseBranch))
		{
			isMin = lessLike;
			return true;
		}

		// b < a ? a : b => Max, b > a ? a : b => Min
		if (comparer.Equals(right, trueBranch) && comparer.Equals(left, falseBranch))
		{
			isMin = !lessLike;
			return true;
		}

		return false;
	}

	private static bool TryGetAbsoluteValuePattern(
		ExpressionSyntax condition,
		ExpressionSyntax whenTrue,
		ExpressionSyntax whenFalse,
		out ExpressionSyntax absoluteValueInput)
	{
		absoluteValueInput = null!;

		var comparer = SyntaxNodeComparer.Get();
		var trueBranch = UnwrapParentheses(whenTrue);
		var falseBranch = UnwrapParentheses(whenFalse);
		var negativeWhenTrue = false;

		if (TryGetNegatedExpression(trueBranch, out var trueNegatedOperand)
		    && comparer.Equals(falseBranch, trueNegatedOperand))
		{
			absoluteValueInput = falseBranch;
			negativeWhenTrue = true;
		}
		else if (TryGetNegatedExpression(falseBranch, out var falseNegatedOperand)
		         && comparer.Equals(trueBranch, falseNegatedOperand))
		{
			absoluteValueInput = trueBranch;
			negativeWhenTrue = false;
		}
		else
		{
			return false;
		}

		if (UnwrapParentheses(condition) is not BinaryExpressionSyntax binary)
		{
			return false;
		}

		var kind = (SyntaxKind) binary.RawKind;

		if (kind is not (SyntaxKind.LessThanExpression
		    or SyntaxKind.LessThanOrEqualExpression
		    or SyntaxKind.GreaterThanExpression
		    or SyntaxKind.GreaterThanOrEqualExpression))
		{
			return false;
		}

		var left = UnwrapParentheses(binary.Left);
		var right = UnwrapParentheses(binary.Right);
		var leftIsZero = left.IsNumericZero();
		var rightIsZero = right.IsNumericZero();

		if (leftIsZero == rightIsZero)
		{
			return false;
		}

		var comparedExpression = leftIsZero ? right : left;

		if (!comparer.Equals(comparedExpression, absoluteValueInput))
		{
			return false;
		}

		var conditionMeansNegative = leftIsZero
			? kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
			: kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression;

		return negativeWhenTrue == conditionMeansNegative;
	}

	private static bool TryGetNegatedExpression(ExpressionSyntax expression, out ExpressionSyntax operand)
	{
		operand = null!;
		var unwrapped = UnwrapParentheses(expression);

		if (unwrapped is not PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } prefix)
		{
			return false;
		}

		operand = UnwrapParentheses(prefix.Operand);
		return true;
	}

	private static bool TryGetClampPattern(
		ExpressionSyntax condition,
		ExpressionSyntax whenTrue,
		ExpressionSyntax whenFalse,
		out ExpressionSyntax value,
		out ExpressionSyntax min,
		out ExpressionSyntax max)
	{
		value = null!;
		min = null!;
		max = null!;

		if (!TryGetBoundConditional(condition, whenTrue, whenFalse, out var outerValue, out var outerBound, out var outerIsLowerBound, out var outerPassThrough))
		{
			return false;
		}

		if (UnwrapParentheses(outerPassThrough) is InvocationExpressionSyntax minMaxInvocation
		    && TryGetMinMaxWithValue(minMaxInvocation, outerValue, out var otherBound, out var isMinNative)
		    && (outerIsLowerBound && isMinNative || !outerIsLowerBound && !isMinNative))
		{
			value = outerValue;
			min = outerIsLowerBound ? outerBound : otherBound;
			max = outerIsLowerBound ? otherBound : outerBound;
			return true;
		}

		if (UnwrapParentheses(outerPassThrough) is not ConditionalExpressionSyntax inner)
		{
			return false;
		}

		if (!TryGetBoundConditional(inner.Condition, inner.WhenTrue, inner.WhenFalse, out var innerValue, out var innerBound, out var innerIsLowerBound, out var innerPassThrough))
		{
			return false;
		}

		var comparer = SyntaxNodeComparer.Get();

		if (!comparer.Equals(outerValue, innerValue)
		    || !comparer.Equals(innerPassThrough, innerValue)
		    || outerIsLowerBound == innerIsLowerBound)
		{
			return false;
		}

		value = outerValue;
		min = outerIsLowerBound ? outerBound : innerBound;
		max = outerIsLowerBound ? innerBound : outerBound;
		return true;
	}

	private static bool TryGetBoundConditional(
		ExpressionSyntax condition,
		ExpressionSyntax whenTrue,
		ExpressionSyntax whenFalse,
		out ExpressionSyntax value,
		out ExpressionSyntax bound,
		out bool isLowerBound,
		out ExpressionSyntax passThrough)
	{
		value = null!;
		bound = null!;
		passThrough = null!;
		isLowerBound = false;

		var comparer = SyntaxNodeComparer.Get();
		var trueBranch = UnwrapParentheses(whenTrue);
		var falseBranch = UnwrapParentheses(whenFalse);

		if (UnwrapParentheses(condition) is not BinaryExpressionSyntax binary)
		{
			return false;
		}

		var kind = (SyntaxKind) binary.RawKind;

		if (kind is not (SyntaxKind.LessThanExpression
		    or SyntaxKind.LessThanOrEqualExpression
		    or SyntaxKind.GreaterThanExpression
		    or SyntaxKind.GreaterThanOrEqualExpression))
		{
			return false;
		}

		var left = UnwrapParentheses(binary.Left);
		var right = UnwrapParentheses(binary.Right);

		if (comparer.Equals(trueBranch, right))
		{
			value = left;
			bound = right;
			isLowerBound = kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression;
			passThrough = falseBranch;
			return true;
		}

		if (comparer.Equals(trueBranch, left))
		{
			value = right;
			bound = left;
			isLowerBound = kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;
			passThrough = falseBranch;
			return true;
		}

		return false;
	}

	private static bool TryGetMinMaxWithValue(
		InvocationExpressionSyntax invocation,
		ExpressionSyntax value,
		out ExpressionSyntax otherBound,
		out bool isMinNative)
	{
		otherBound = null!;
		isMinNative = false;

		if (invocation.Expression is not MemberAccessExpressionSyntax member
		    || member.Name.Identifier.Text is not ("MinNative" or "MaxNative" or "Min" or "Max")
		    || invocation.ArgumentList.Arguments.Count != 2)
		{
			return false;
		}

		var comparer = SyntaxNodeComparer.Get();
		var first = UnwrapParentheses(invocation.ArgumentList.Arguments[0].Expression);
		var second = UnwrapParentheses(invocation.ArgumentList.Arguments[1].Expression);

		if (comparer.Equals(first, value))
		{
			otherBound = second;
		}
		else if (comparer.Equals(second, value))
		{
			otherBound = first;
		}
		else
		{
			return false;
		}

		isMinNative = member.Name.Identifier.Text is "MinNative" or "Min";
		return true;
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