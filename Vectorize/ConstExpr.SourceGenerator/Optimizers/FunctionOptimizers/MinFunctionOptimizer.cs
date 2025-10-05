using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class MinFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		// Support Min on System.Math/System.MathF and also on the numeric type helper (e.g., Single.Min, Double.Min, Int32.Min, ...)
		if (method.Name != "Min")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName; // e.g., Single.Min(float, float)

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		// Try to recognize Clamp pattern: Min(Max(X, min), max) -> Clamp(X, min, max)
		if (parameters.Count == 2)
		{
			if (TryRewriteClampFromMinMax(paramType, floatingPointMode, containingName, parameters[0], parameters[1], out var clamp))
			{
				result = clamp;
				return true;
			}

			if (TryRewriteClampFromMinMax(paramType, floatingPointMode, containingName, parameters[1], parameters[0], out clamp))
			{
				result = clamp;
				return true;
			}
		}

		// Try to flatten nested Min with constants: Min(C1, Min(X, C2)) -> Min(X, min(C1, C2)) and symmetrical forms
		if (parameters.Count == 2)
		{
			if (TryFlattenNestedMin(paramType, containingName, parameters[0], parameters[1], out var flattened))
			{
				result = flattened;
				return true;
			}

			if (TryFlattenNestedMin(paramType, containingName, parameters[1], parameters[0], out flattened))
			{
				result = flattened;
				return true;
			}
		}

		if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "MinNative", 2))
		{
			// Use MaxNative if available on the numeric helper type
			result = CreateInvocation(paramType, "MinNative", parameters[0], parameters[1]);
			return true;
		}

		// Fallback: just re-target to the numeric helper (ensures nested Single.Max(...) is supported)
		result = CreateInvocation(paramType!, "Min", parameters[0], parameters[1]);
		return true;
	}

	private bool TryFlattenNestedMin(ITypeSymbol paramType, string? outerContainingName, ExpressionSyntax first, ExpressionSyntax second, out InvocationExpressionSyntax? result)
	{
		result = null;

		// We want pattern: first is constant, second is invocation of Min(...)
		if (!TryGetConstantValue(paramType, first, out var c1, out var c1Expr))
		{
			return false;
		}

		if (second is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Min" or "MinNative" } member } innerInv)
		{
			return false;
		}

		// Make sure the inner Min belongs to the same helper (by name) when available, to avoid crossing types
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
			// Inner is Min of two constants: keep the smaller literal as the inner result
			var pickA = Compare(paramType, c2a!, c2b!) <= 0;
			innerConstExpr = pickA ? c2aExpr : c2bExpr;
			innerConstValue = pickA ? c2a : c2b;
			nonConst = null;
		}
		else
		{
			return false; // cannot safely flatten if inner has two non-constants
		}

		// If both outer and inner become constants -> cannot produce an invocation here
		if (nonConst is null && innerConstExpr is not null)
		{
			var pickOuter = Compare(paramType, c1!, innerConstValue!) <= 0;
			var _ = pickOuter ? c1Expr : innerConstExpr;
			// Returning constant here would change return type; signal no transform
			result = null;
			return false;
		}

		// Choose the smaller of the two constants
		var pickOuterConst = Compare(paramType, c1!, innerConstValue!) <= 0;
		var smallerConstExpr = pickOuterConst ? c1Expr : innerConstExpr!;

		// Preserve evaluation safety: moving a constant across boundaries has no side-effects
		result = CreateInvocation(paramType, "Min", nonConst!, smallerConstExpr);
		return true;
	}

	private bool TryRewriteClampFromMinMax(ITypeSymbol paramType, FloatingPointEvaluationMode floatingPointMode, string? outerContainingName, ExpressionSyntax first, ExpressionSyntax second, out InvocationExpressionSyntax? result)
	{
		result = null;

		// Pattern 1: Min(Max(X, minConst), maxConst)
		if (first is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" } maxMember } maxInv
		    && TryGetConstantValue(paramType, second, out var maxConstVal, out var maxConstExpr))
		{
			// Ensure the inner Max belongs to the same numeric helper
			if (outerContainingName is not null)
			{
				var innerContainerName = maxMember.Expression switch
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

			var args = maxInv.ArgumentList.Arguments;
			if (args.Count != 2) return false;

			var m0 = args[0].Expression;
			var m1 = args[1].Expression;

			var hasMinC0 = TryGetConstantValue(paramType, m0, out var minValA, out var minExprA);
			var hasMinC1 = TryGetConstantValue(paramType, m1, out var minValB, out var minExprB);

			ExpressionSyntax? valueExpr = null;
			ExpressionSyntax? minExpr = null;
			object? minVal = null;

			if (hasMinC0 && !hasMinC1)
			{
				valueExpr = m1;
				minExpr = minExprA;
				minVal = minValA;
			}
			else if (!hasMinC0 && hasMinC1)
			{
				valueExpr = m0;
				minExpr = minExprB;
				minVal = minValB;
			}
			else
			{
				return false; // inner must have exactly one constant bound
			}

			// Bounds must be ordered min <= max to preserve semantics
			if (Compare(paramType, minVal!, maxConstVal!) <= 0)
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "ClampNative", 2))
				{
					// Use MaxNative if available on the numeric helper type
					result = CreateInvocation(paramType, "ClampNative", valueExpr!, minExpr!, maxConstExpr!);
					return true;
				}

				// Fallback: just re-target to the numeric helper (ensures nested Single.Max(...) is supported)
				result = CreateInvocation(paramType!, "Clamp", valueExpr!, minExpr!, maxConstExpr!);
				return true;
			}
		}

		// Pattern 2: Min(maxConst, Max(X, minConst))
		if (second is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" } maxMember2 } maxInv2
		    && TryGetConstantValue(paramType, first, out var maxConstVal2, out var maxConstExpr2))
		{
			if (outerContainingName is not null)
			{
				var innerContainerName = maxMember2.Expression switch
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

			var args2 = maxInv2.ArgumentList.Arguments;
			if (args2.Count != 2) return false;

			var mm0 = args2[0].Expression;
			var mm1 = args2[1].Expression;

			var hasMinC0b = TryGetConstantValue(paramType, mm0, out var minValA2, out var minExprA2);
			var hasMinC1b = TryGetConstantValue(paramType, mm1, out var minValB2, out var minExprB2);

			ExpressionSyntax? valueExpr2 = null;
			ExpressionSyntax? minExpr2 = null;
			object? minVal2 = null;

			if (hasMinC0b && !hasMinC1b)
			{
				valueExpr2 = mm1;
				minExpr2 = minExprA2;
				minVal2 = minValA2;
			}
			else if (!hasMinC0b && hasMinC1b)
			{
				valueExpr2 = mm0;
				minExpr2 = minExprB2;
				minVal2 = minValB2;
			}
			else
			{
				return false;
			}

			if (Compare(paramType, minVal2!, maxConstVal2!) <= 0)
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "ClampNative", 2))
				{
					// Use MaxNative if available on the numeric helper type
					result = CreateInvocation(paramType, "ClampNative", valueExpr2!, minExpr2!, maxConstExpr2!);
					return true;
				}

				// Fallback: just re-target to the numeric helper (ensures nested Single.Max(...) is supported)
				result = CreateInvocation(paramType!, "Clamp", valueExpr2!, minExpr2!, maxConstExpr2!);
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
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax opLit }:
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
			var dec = System.Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture);
			return -dec;
		}
		catch
		{
			try
			{
				var dbl = System.Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
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
		try { return (T) System.Convert.ChangeType(v, typeof(T), System.Globalization.CultureInfo.InvariantCulture); }
		catch { return default!; }
	}
}