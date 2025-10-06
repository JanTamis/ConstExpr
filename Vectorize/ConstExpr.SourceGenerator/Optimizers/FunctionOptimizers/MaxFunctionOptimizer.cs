using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Globalization;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class MaxFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		// Support Max on System.Math/System.MathF and also on the numeric type helper (e.g., Single.Max, Double.Max, Int32.Max, ...)
		if (method.Name != "Max")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName; // e.g., Single.Max(float, float)

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		// Try to recognize Clamp pattern: Max(Min(X, max), min) -> Clamp(X, min, max)
		if (parameters.Count == 2)
		{
			if (TryRewriteClampFromMaxMin(paramType, floatingPointMode, containingName, parameters[0], parameters[1], out var clamp))
			{
				result = clamp;
				return true;
			}
			if (TryRewriteClampFromMaxMin(paramType, floatingPointMode, containingName, parameters[1], parameters[0], out clamp))
			{
				result = clamp;
				return true;
			}
		}

		// Try to flatten nested Max with constants: Max(C1, Max(X, C2)) -> Max(X, max(C1, C2)) and symmetrical forms
		if (parameters.Count == 2)
		{
			if (TryFlattenNestedMax(paramType, containingName, parameters[0], parameters[1], out var flattened))
			{
				result = flattened;
				return true;
			}
			if (TryFlattenNestedMax(paramType, containingName, parameters[1], parameters[0], out flattened))
			{
				result = flattened;
				return true;
			}
		}

		if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "MaxNative", 2))
		{
			// Use MaxNative if available on the numeric helper type
			result = CreateInvocation(paramType, "MaxNative", parameters[0], parameters[1]);
			return true;
		}

		// Fallback: just re-target to the numeric helper (ensures nested Single.Max(...) is supported)
		result = CreateInvocation(paramType!, "Max", parameters[0], parameters[1]);
		return true;
	}

	private bool TryFlattenNestedMax(ITypeSymbol paramType, string? outerContainingName, ExpressionSyntax first, ExpressionSyntax second, out InvocationExpressionSyntax? result)
	{
		result = null;

		// We want pattern: first is constant, second is invocation of Max(...)
		if (!TryGetConstantValue(paramType, first, out var c1, out var c1Expr))
		{
			return false;
		}

		if (second is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" } member } innerInv)
		{
			return false;
		}

		// Make sure the inner Max belongs to the same helper (by name) when available, to avoid crossing types
		if (outerContainingName is not null)
		{
			var innerContainerName = member.Expression switch
			{
				IdentifierNameSyntax id => id.Identifier.Text,
				QualifiedNameSyntax qn => qn.Right.Identifier.Text,
				_ => null
			};

			if (innerContainerName is not null && innerContainerName != paramType.Name)
			{
				return false;
			}
		}

		var args = innerInv.ArgumentList.Arguments;
		if (args.Count != 2)
		{
			return false;
		}

		var a0 = args[0].Expression;
		var a1 = args[1].Expression;

		var hasC0 = TryGetConstantValue(paramType, a0, out var c2a, out var c2aExpr);
		var hasC1 = TryGetConstantValue(paramType, a1, out var c2b, out var c2bExpr);

		ExpressionSyntax? nonConst = null;
		ExpressionSyntax? innerConstExpr = null;
		object? innerConstValue = null;

		if (hasC0 && !hasC1)
		{
			nonConst = a1;
			innerConstExpr = c2aExpr;
			innerConstValue = c2a;
		}
		else if (!hasC0 && hasC1)
		{
			nonConst = a0;
			innerConstExpr = c2bExpr;
			innerConstValue = c2b;
		}
		else if (hasC0 && hasC1)
		{
			// Inner is Max of two constants: keep the larger literal as the inner result
			var pickA = Compare(paramType, c2a!, c2b!) >= 0;
			innerConstExpr = pickA ? c2aExpr : c2bExpr;
			innerConstValue = pickA ? c2a : c2b;
			nonConst = null;
		}
		else
		{
			return false; // cannot safely flatten if inner has two non-constants
		}

		// If both outer and inner become constants -> return the larger constant directly
		if (nonConst is null && innerConstExpr is not null)
		{
			var pickOuter = Compare(paramType, c1!, innerConstValue!) >= 0;
			var chosen = pickOuter ? c1Expr : innerConstExpr;
			// Wrap chosen as the full expression result (no invocation needed)
			result = CreateInvocation(paramType, "Max", chosen, chosen);
			// But returning Max(x, x) would be redundant; instead, signal to caller we cannot produce invocation
			result = null;
			return false;
		}

		// Choose the larger of the two constants
		var pickOuterConst = Compare(paramType, c1!, innerConstValue!) >= 0;
		var largerConstExpr = pickOuterConst ? c1Expr : innerConstExpr!;

		// Preserve evaluation safety: moving a constant across boundaries has no side-effects
		result = CreateInvocation(paramType, "Max", nonConst!, largerConstExpr);
		return true;
	}

	private bool TryRewriteClampFromMaxMin(ITypeSymbol paramType, FloatingPointEvaluationMode floatingPointMode, string? outerContainingName, ExpressionSyntax first, ExpressionSyntax second, out InvocationExpressionSyntax? result)
	{
		result = null;

		// Pattern 1: Max(Min(X, maxConst), minConst)
		if (first is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Min" or "MinNative" } minMember } minInv
			&& TryGetConstantValue(paramType, second, out var minConstVal, out var minConstExpr))
		{
			// Ensure the inner Min belongs to the same numeric helper
			if (outerContainingName is not null)
			{
				var innerContainerName = minMember.Expression switch
				{
					IdentifierNameSyntax id => id.Identifier.Text,
					QualifiedNameSyntax qn => qn.Right.Identifier.Text,
					_ => null
				};
				if (innerContainerName is not null && innerContainerName != paramType.Name)
				{
					return false;
				}
			}

			var args = minInv.ArgumentList.Arguments;
			if (args.Count != 2) return false;

			var m0 = args[0].Expression;
			var m1 = args[1].Expression;

			var hasMaxC0 = TryGetConstantValue(paramType, m0, out var maxValA, out var maxExprA);
			var hasMaxC1 = TryGetConstantValue(paramType, m1, out var maxValB, out var maxExprB);

			ExpressionSyntax? valueExpr = null;
			ExpressionSyntax? maxExpr = null;
			object? maxVal = null;

			if (hasMaxC0 && !hasMaxC1)
			{
				valueExpr = m1;
				maxExpr = maxExprA;
				maxVal = maxValA;
			}
			else if (!hasMaxC0 && hasMaxC1)
			{
				valueExpr = m0;
				maxExpr = maxExprB;
				maxVal = maxValB;
			}
			else
			{
				return false; // inner must have exactly one constant bound
			}

			// Bounds must be ordered min <= max to preserve semantics
			if (Compare(paramType, minConstVal!, maxVal!) <= 0)
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "ClampNative", 3))
				{
					result = CreateInvocation(paramType, "ClampNative", valueExpr!, minConstExpr!, maxExpr!);
					return true;
				}

				result = CreateInvocation(paramType!, "Clamp", valueExpr!, minConstExpr!, maxExpr!);
				return true;
			}
		}

		// Pattern 2: Max(minConst, Min(X, maxConst))
		if (second is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Min" or "MinNative" } minMember2 } minInv2
			&& TryGetConstantValue(paramType, first, out var minConstVal2, out var minConstExpr2))
		{
			if (outerContainingName is not null)
			{
				var innerContainerName = minMember2.Expression switch
				{
					IdentifierNameSyntax id => id.Identifier.Text,
					QualifiedNameSyntax qn => qn.Right.Identifier.Text,
					_ => null
				};

				if (innerContainerName is not null && innerContainerName != paramType.Name)
				{
					return false;
				}
			}

			var args2 = minInv2.ArgumentList.Arguments;
			if (args2.Count != 2) return false;

			var mm0 = args2[0].Expression;
			var mm1 = args2[1].Expression;

			var hasMaxC0b = TryGetConstantValue(paramType, mm0, out var maxValA2, out var maxExprA2);
			var hasMaxC1b = TryGetConstantValue(paramType, mm1, out var maxValB2, out var maxExprB2);

			ExpressionSyntax? valueExpr2 = null;
			ExpressionSyntax? maxExpr2 = null;
			object? maxVal2 = null;

			if (hasMaxC0b && !hasMaxC1b)
			{
				valueExpr2 = mm1;
				maxExpr2 = maxExprA2;
				maxVal2 = maxValA2;
			}
			else if (!hasMaxC0b && hasMaxC1b)
			{
				valueExpr2 = mm0;
				maxExpr2 = maxExprB2;
				maxVal2 = maxValB2;
			}
			else
			{
				return false;
			}

			if (Compare(paramType, minConstVal2!, null!) <= 0)
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "ClampNative", 3))
				{
					result = CreateInvocation(paramType, "ClampNative", valueExpr2!, minConstExpr2!, maxExpr2!);
					return true;
				}

				result = CreateInvocation(paramType!, "Clamp", valueExpr2!, minConstExpr2!, maxExpr2!);
				return true;
			}
		}

		return false;
	}

	private static bool TryGetConstantValue(ITypeSymbol paramType, ExpressionSyntax expr, out object? value, out ExpressionSyntax? constExpr)
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
				constExpr = expr; // keep the original syntax including the minus
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