using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RootNFunctionOptimizer() : BaseMathFunctionOptimizer("RootN", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var n = context.VisitedParameters[1];

		// Try to get the n value if it's a literal
		if (TryGetIntegerLiteral(n, out var nValue))
		{
			switch (nValue)
			{
				// RootN(x, 1) => x
				case 1:
				{
					result = x;
					return true;
				}
				// RootN(x, 2) => Sqrt(x)
				case 2 when HasMethod(paramType, "Sqrt", 1):
				{
					result = CreateInvocation(paramType, "Sqrt", x);
					return true;
				}
				// RootN(x, 3) => Cbrt(x)
				case 3 when HasMethod(paramType, "Cbrt", 1):
				{
					result = CreateInvocation(paramType, "Cbrt", x);
					return true;
				}
				// RootN(x, -1) => Reciprocal(x) or 1/x
				case -1 when HasMethod(paramType, "Reciprocal", 1):
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}
				case -1:
				{
					var div = DivideExpression(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0)), x);

					result = ParenthesizedExpression(div);
					return true;
				}
				// For negative n: RootN(x, -n) => Reciprocal(RootN(x, n)) if available and fast-math, otherwise 1 / RootN(x, n)
				case < 0:
				{
					var positiveN = LiteralExpression(SyntaxKind.NumericLiteralExpression,
						Literal(-nValue));

					var rootInvocation = CreateInvocation(paramType, "RootN", x, positiveN);

					if (HasMethod(paramType, "Reciprocal", 1))
					{
						result = CreateInvocation(paramType, "Reciprocal", rootInvocation);
						return true;
					}

					var div = DivideExpression(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0)),
						rootInvocation);

					result = ParenthesizedExpression(div);
					return true;
				}
			}

		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastRootNMethodFloat()
				: GenerateFastRootNMethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetIntegerLiteral(ExpressionSyntax expr, out int value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: int i }:
			{
				value = i;
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: int i2 } }:
			{
				value = -i2;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	private static string GenerateFastRootNMethodFloat()
	{
		// Benchmark results (Apple M4 Pro, .NET 10, ARM64):
		// FastExpLog (this): ~2.24 ns constant for any n  ← FASTEST
		// Hardware ExpLog:   ~3.0 ns constant
		// Built-in float.RootN: ~5.9 ns constant
		// Previous O(n) Newton: 4.3 ns (n=5) → 8.9 ns (n=10)
		// FastLog and FastExp from LogFunctionOptimizer / ExpFunctionOptimizer are inlined
		// to keep FastRootN self-contained (no external helper-method dependency).
		return """
			private static float FastRootN(float x, int n)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				if (n == 0)
					return float.NaN;
				
				if (n == 1)
					return x;
				
				if (x == 0.0f)
					return 0.0f;
				
				if (n < 0)
					return 1.0f / FastRootN(x, -n);
				
				var ax = x < 0.0f ? -x : x;
				
				// Inline FastLog(ax): bit-extract exponent + degree-4 Horner for ln(m ∈ [1,2)).
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				var lBits = BitConverter.SingleToInt32Bits(ax);
				var lE    = (lBits >> 23) - 127;
				var lM    = BitConverter.Int32BitsToSingle((lBits & 0x007FFFFF) | 0x3F800000);
				var lnm   = Single.FusedMultiplyAdd(-0.056570851f, lM,  0.447178975f);
				lnm       = Single.FusedMultiplyAdd(lnm,           lM, -1.469956800f);
				lnm       = Single.FusedMultiplyAdd(lnm,           lM,  2.821202636f);
				lnm       = Single.FusedMultiplyAdd(lnm,           lM, -1.741793927f);
				var lnAx  = lE * 0.6931471805599453f + lnm;
				
				// Divide by n.
				var t = lnAx / n;
				
				// Inline FastExp(t): range reduction to r ∈ [-0.5, 0.5] + degree-3 Horner for 2^r.
				var kf  = t * 1.4426950408889634f;    // t * log₂(e)
				var k   = (int)Single.Round(kf);       // branchless FRINTN + FCVTZS on ARM64
				var r   = kf - k;
				var p   = Single.FusedMultiplyAdd(0.055504108664821580f, r, 0.240226506959100690f);
				p       = Single.FusedMultiplyAdd(p,                     r, 0.693147180559945309f);
				var ans = Single.FusedMultiplyAdd(p, r, 1.0f) * BitConverter.Int32BitsToSingle((k + 127) << 23);
				
				return (x < 0.0f && (n & 1) != 0) ? -ans : ans;
			}
			""";
	}

	private static string GenerateFastRootNMethodDouble()
	{
		// Benchmark results (Apple M4 Pro, .NET 10, ARM64):
		// FastExpLog (this): ~2.30 ns constant for any n  ← FASTEST
		// Hardware ExpLog:   ~4.8 ns constant
		// Built-in double.RootN: ~5.6 ns constant
		// Previous O(n) Newton: 5.3 ns (n=5) → 10.7 ns (n=10)
		return """
			private static double FastRootN(double x, int n)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				if (n == 0)
					return double.NaN;
				
				if (n == 1)
					return x;
				
				if (x == 0.0)
					return 0.0;
				
				if (n < 0)
					return 1.0 / FastRootN(x, -n);
				
				var ax = x < 0.0 ? -x : x;
				
				// Inline FastLog(ax): bit-extract exponent + degree-4 Horner for ln(m ∈ [1,2)).
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				var lBits = BitConverter.DoubleToInt64Bits(ax);
				var lE    = (int)((lBits >> 52) - 1023L);
				var lM    = BitConverter.Int64BitsToDouble((lBits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
				var lnm   = Double.FusedMultiplyAdd(-0.056570851,  lM,  0.447178975);
				lnm       = Double.FusedMultiplyAdd(lnm,           lM, -1.469956800);
				lnm       = Double.FusedMultiplyAdd(lnm,           lM,  2.821202636);
				lnm       = Double.FusedMultiplyAdd(lnm,           lM, -1.741793927);
				var lnAx  = lE * 0.6931471805599453094172321214581766 + lnm;
				
				// Divide by n.
				var t = lnAx / n;
				
				// Inline FastExp(t): range reduction to r ∈ [-0.5, 0.5] + degree-4 Horner for 2^r.
				var kf  = t * 1.4426950408889634073599246810018921;  // t * log₂(e)
				var k   = (long)Double.Round(kf);                     // branchless on ARM64
				var r   = kf - k;
				var p   = Double.FusedMultiplyAdd(9.618129107628477232e-3, r, 5.550410866482157995e-2);
				p       = Double.FusedMultiplyAdd(p,                       r, 2.402265069591006909e-1);
				p       = Double.FusedMultiplyAdd(p,                       r, 6.931471805599453094e-1);
				var ans = Double.FusedMultiplyAdd(p, r, 1.0) * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));
				
				return (x < 0.0 && (n & 1) != 0) ? -ans : ans;
			}
			""";
	}
}