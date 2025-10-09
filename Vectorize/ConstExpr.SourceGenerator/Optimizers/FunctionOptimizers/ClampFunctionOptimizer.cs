using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Globalization;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class ClampFunctionOptimizer() : BaseFunctionOptimizer("Clamp", 3)
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		var value = parameters[0];
		var min = parameters[1];
		var max = parameters[2];

		// 1) Idempotence: Clamp(Clamp(x, min, max), min, max) -> Clamp(x, min, max)
		if (value is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Clamp" or "ClampNative" } } innerClamp)
		{
			var innerArgs = innerClamp.ArgumentList.Arguments;

			if (innerArgs.Count == 3)
			{
				var innerMin = innerArgs[1].Expression;
				var innerMax = innerArgs[2].Expression;

				// Check if bounds are the same (syntactically)
				if (AreSyntacticallyEqual(min, innerMin) && AreSyntacticallyEqual(max, innerMax))
				{
					result = innerClamp;
					return true;
				}
			}
		}

		// 2) Constant bounds: if min == max, return that constant (wrapped in appropriate context)
		if (TryGetConstantValue(paramType, min, out var minVal, out var minExpr) &&
				TryGetConstantValue(paramType, max, out var maxVal, out _))
		{
			if (Compare(paramType, minVal!, maxVal!) == 0)
			{
				// Clamp(x, c, c) -> c
				result = minExpr;
				return true;
			}

			// Ensure min <= max; if not, this is invalid usage but we can't fix it here
			if (Compare(paramType, minVal!, maxVal!) > 0)
			{
				// Invalid clamp bounds; keep as-is or swap? For safety, keep as-is
			}
		}

		// 3) Nested Min/Max patterns: Clamp(Min(x, maxConst), minConst, maxConst) -> Clamp(x, minConst, maxConst)
		if (value is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Min" or "MinNative" } } minInv)
		{
			if (TrySimplifyClampWithMin(paramType, minInv, min, max, out var simplified))
			{
				result = simplified;
				return true;
			}
		}

		// 4) Nested Max patterns: Clamp(Max(x, minConst), minConst, maxConst) -> Clamp(x, minConst, maxConst)
		if (value is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" } } maxInv)
		{
			if (TrySimplifyClampWithMax(paramType, maxInv, min, max, out var simplified))
			{
				result = simplified;
				return true;
			}
		}

		// 5) FastMath: use ClampNative if available
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "ClampNative", 3))
		{
			result = CreateInvocation(paramType, "ClampNative", value, min, max);
			return true;
		}

		// Fallback: just re-target to the numeric helper
		result = CreateInvocation(paramType, Name, value, min, max);
		return true;
	}

	private bool TrySimplifyClampWithMin(ITypeSymbol paramType, InvocationExpressionSyntax minInv, ExpressionSyntax outerMin, ExpressionSyntax outerMax, out InvocationExpressionSyntax? result)
	{
		result = null;

		var args = minInv.ArgumentList.Arguments;
		if (args.Count != 2) return false;

		var m0 = args[0].Expression;
		var m1 = args[1].Expression;

		// Pattern: Clamp(Min(x, maxConst), minConst, maxConst) where maxConst in Min matches outerMax
		var hasC0 = TryGetConstantValue(paramType, m0, out _, out var c0Expr);
		var hasC1 = TryGetConstantValue(paramType, m1, out _, out var c1Expr);

		ExpressionSyntax? valueExpr;
		ExpressionSyntax? innerMaxExpr;

		if (hasC0 && !hasC1)
		{
			valueExpr = m1;
			innerMaxExpr = c0Expr;
		}
		else if (!hasC0 && hasC1)
		{
			valueExpr = m0;
			innerMaxExpr = c1Expr;
		}
		else
		{
			return false;
		}

		// Check if innerMaxExpr matches outerMax
		if (innerMaxExpr is not null && AreSyntacticallyEqual(innerMaxExpr, outerMax))
		{
			result = CreateInvocation(paramType, Name, valueExpr, outerMin, outerMax);
			return true;
		}

		return false;
	}

	private bool TrySimplifyClampWithMax(ITypeSymbol paramType, InvocationExpressionSyntax maxInv, ExpressionSyntax outerMin, ExpressionSyntax outerMax, out InvocationExpressionSyntax? result)
	{
		result = null;

		var args = maxInv.ArgumentList.Arguments;
		if (args.Count != 2) return false;

		var m0 = args[0].Expression;
		var m1 = args[1].Expression;

		// Pattern: Clamp(Max(x, minConst), minConst, maxConst) where minConst in Max matches outerMin
		var hasC0 = TryGetConstantValue(paramType, m0, out _, out var c0Expr);
		var hasC1 = TryGetConstantValue(paramType, m1, out _, out var c1Expr);

		ExpressionSyntax? valueExpr;
		ExpressionSyntax? innerMinExpr;

		if (hasC0 && !hasC1)
		{
			valueExpr = m1;
			innerMinExpr = c0Expr;
		}
		else if (!hasC0 && hasC1)
		{
			valueExpr = m0;
			innerMinExpr = c1Expr;
		}
		else
		{
			return false;
		}

		// Check if innerMinExpr matches outerMin
		if (innerMinExpr is not null && AreSyntacticallyEqual(innerMinExpr, outerMin))
		{
			result = CreateInvocation(paramType, Name, valueExpr, outerMin, outerMax);
			return true;
		}

		return false;
	}

	private static bool AreSyntacticallyEqual(ExpressionSyntax a, ExpressionSyntax b)
	{
		return SyntaxFactory.AreEquivalent(a, b);
	}

	private static bool TryGetConstantValue(ITypeSymbol _, ExpressionSyntax expr, out object? value, out ExpressionSyntax? constExpr)
	{
		value = null;
		constExpr = null;

		switch (expr)
		{
			case LiteralExpressionSyntax lit:
				value = lit.Token.Value;
				constExpr = expr;
				return value is not null && IsNumericLiteral(value);
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax opLit }:
				var v = opLit.Token.Value;
				if (v is null || !IsNumericLiteral(v)) return false;
				value = NegateNumeric(v);
				constExpr = expr;
				return true;
			default:
				return false;
		}
	}

	private static bool IsNumericLiteral(object v) => v is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

	private static object NegateNumeric(object v)
	{
		try
		{
			var dec = System.Convert.ToDecimal(v, CultureInfo.InvariantCulture);
			return -dec;
		}
		catch
		{
			try
			{
				var dbl = System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
				return -dbl;
			}
			catch { return v; }
		}
	}

	private static int Compare(ITypeSymbol paramType, object a, object b)
	{
		switch (paramType.SpecialType)
		{
			case SpecialType.System_Single:
				return Comparer<float>.Default.Compare(ConvertTo<float>(a), ConvertTo<float>(b));
			case SpecialType.System_Double:
				return Comparer<double>.Default.Compare(ConvertTo<double>(a), ConvertTo<double>(b));
			case SpecialType.System_Decimal:
				return Comparer<decimal>.Default.Compare(ConvertTo<decimal>(a), ConvertTo<decimal>(b));
			case SpecialType.System_SByte:
				return Comparer<sbyte>.Default.Compare(ConvertTo<sbyte>(a), ConvertTo<sbyte>(b));
			case SpecialType.System_Int16:
				return Comparer<short>.Default.Compare(ConvertTo<short>(a), ConvertTo<short>(b));
			case SpecialType.System_Int32:
				return Comparer<int>.Default.Compare(ConvertTo<int>(a), ConvertTo<int>(b));
			case SpecialType.System_Int64:
				return Comparer<long>.Default.Compare(ConvertTo<long>(a), ConvertTo<long>(b));
			case SpecialType.System_Byte:
				return Comparer<byte>.Default.Compare(ConvertTo<byte>(a), ConvertTo<byte>(b));
			case SpecialType.System_UInt16:
				return Comparer<ushort>.Default.Compare(ConvertTo<ushort>(a), ConvertTo<ushort>(b));
			case SpecialType.System_UInt32:
				return Comparer<uint>.Default.Compare(ConvertTo<uint>(a), ConvertTo<uint>(b));
			case SpecialType.System_UInt64:
				return Comparer<ulong>.Default.Compare(ConvertTo<ulong>(a), ConvertTo<ulong>(b));
			default:
				// Fallback: compare as double
				return Comparer<double>.Default.Compare(ConvertTo<double>(a), ConvertTo<double>(b));
		}
	}

	private static T ConvertTo<T>(object v)
	{
		try { return (T)System.Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture); }
		catch { return default!; }
	}
}
