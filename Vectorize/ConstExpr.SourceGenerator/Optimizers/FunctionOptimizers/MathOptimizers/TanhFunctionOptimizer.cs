using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
/// Optimizer for Math.Tanh / MathF.Tanh.
///
/// Implementation strategy (benchmarked on Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///
///   Float  – Pure FastExp path: tanh(x) = (FastExp(2x)−1)/(FastExp(2x)+1), saturated at ±5.
///            FastExp inlined from direct-poly V2 (ln(2)ⁿ/n! coefficients, MathF.Round reduction).
///            Eliminates the inner branch of the old hybrid; |2x| ≤ 10, safely within FastExp domain.
///            Result: ~1.75 ns vs 2.12 ns .NET (−17%).  Old hybrid was ~1.94 ns (−9%).
///
///   Double – FastExp hybrid: Padé rational for |x| &lt; 1 (fastest for small inputs with good
///            branch prediction), inlined FastExpDouble (direct-poly V2) for |x| ≥ 1.
///            The old hybrid used Double.Exp (built-in) and was actually SLOWER than .NET
///            on random [-4,4] data.  Replacing it with inlined FastExpDouble gives −4% over .NET.
///            Result: ~2.50 ns vs 2.60 ns .NET (−4%).  Old hybrid was ~2.65 ns (+2%).
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT, uniform [-4,4] input):
///   Method              Float     Ratio   Double    Ratio
///   ------------------  --------  ------  --------  ------
///   DotNetTanh          2.123 ns  1.00x   2.595 ns  1.00x
///   OldFastTanh         1.942 ns  0.91x   2.647 ns  1.02x  ← was SLOWER for double
///   FastTanh (new)      1.753 ns  0.83x   2.496 ns  0.96x  ← production
/// </summary>
public class TanhFunctionOptimizer() : BaseMathFunctionOptimizer("Tanh", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// Tanh(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tanh(Infinity) => 1
			if (double.IsPositiveInfinity(value))
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tanh(-Infinity) => -1
			if (double.IsNegativeInfinity(value))
			{
				result = CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanhMethodFloat()
				: GenerateFastTanhMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTanh", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Pure FastExp path: tanh(x) = (FastExp(2x)−1)/(FastExp(2x)+1), saturated at ±5.
	/// FastExp is inlined (direct-poly V2, ln(2)ⁿ/n! Horner, MathF.Round reduction).
	/// Saturation guarantees |2x| ≤ 10, safely inside FastExp's domain (-87..88).
	/// No inner branch → no branch mispredictions on random mixed-sign data.
	/// ~17% faster than MathF.Tanh; old hybrid with Single.Exp was only ~9% faster.
	/// </summary>
	private static string GenerateFastTanhMethodFloat()
	{
		return """
			private static float FastTanh(float x)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				if (x >= 5.0f) return 1.0f;
				if (x <= -5.0f) return -1.0f;
				
				// Inline FastExp(2x) — direct-poly V2.
				// |2x| ≤ 10, well inside FastExp safe domain (-87..88).
				const float INV_LN2 = 1.4426950408889634f;   // log₂(e)
				var fx   = 2.0f * x;
				var kf   = fx * INV_LN2;
				var k    = (int)Single.Round(kf);             // branchless FRINTN + FCVTZS on ARM64
				var r    = kf - k;                            // r ∈ [-0.5, 0.5]
				
				// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!
				const float c3 = 0.055504108664821580f;   // ln(2)³ / 6
				const float c2 = 0.240226506959100690f;   // ln(2)² / 2
				const float c1 = 0.693147180559945309f;   // ln(2)
				var p     = Single.FusedMultiplyAdd(c3, r, c2);
				p         = Single.FusedMultiplyAdd(p,  r, c1);
				var exp2x = Single.FusedMultiplyAdd(p,  r, 1.0f)
				          * BitConverter.Int32BitsToSingle((k + 127) << 23);
				
				return (exp2x - 1.0f) / (exp2x + 1.0f);
			}
			""";
	}

	/// <summary>
	/// FastExp hybrid: Padé rational for |x| &lt; 1 (no transcendental call),
	/// then inlined FastExpDouble (direct-poly V2) for |x| ≥ 1.
	/// Old implementation used Double.Exp (slow) and was ~2% SLOWER than Math.Tanh on random data.
	/// Inlined FastExpDouble is ~2.8× faster than Double.Exp, recovering the advantage.
	/// ~4% faster than Math.Tanh; old implementation was ~2% SLOWER.
	/// </summary>
	private static string GenerateFastTanhMethodDouble()
	{
		return """
			private static double FastTanh(double x)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				if (x >= 19.0) return 1.0;
				if (x <= -19.0) return -1.0;
				
				var absX = Double.Abs(x);
				
				if (absX < 1.0)
				{
					// [5,6] Padé rational — no transcendental call.
					var x2 = x * x;
					var a1 = -0.333333333333331;
					var a2 =  0.133333333333197;
					var a3 = -0.0539682539682505;
					var numerator = Double.FusedMultiplyAdd(a3, x2, a2);
					numerator = Double.FusedMultiplyAdd(numerator, x2, a1);
					numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
					numerator *= x;
					var b1 =  1.0;
					var b2 = -0.133333333333197;
					var b3 =  0.0107936507936338;
					var denominator = Double.FusedMultiplyAdd(b3, x2, b2);
					denominator = Double.FusedMultiplyAdd(denominator, x2, b1);
					denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);
					return numerator / denominator;
				}
				
				// Inline FastExp(2x) — direct-poly V2.
				// |2x| ≤ 38 (since |x| ≤ 19), well inside domain (-708..709).
				const double INV_LN2 = 1.4426950408889634073599246810018921;   // log₂(e)
				var fx   = 2.0 * x;
				var kf   = fx * INV_LN2;
				var k    = (long)Double.Round(kf);                              // branchless on ARM64
				var r    = kf - k;                                              // r ∈ [-0.5, 0.5]
				
				// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!
				const double c4 = 9.618129107628477232e-3;   // ln(2)⁴ / 24
				const double c3 = 5.550410866482157995e-2;   // ln(2)³ / 6
				const double c2 = 2.402265069591006909e-1;   // ln(2)² / 2
				const double c1 = 6.931471805599453094e-1;   // ln(2)
				var p     = Double.FusedMultiplyAdd(c4, r, c3);
				p         = Double.FusedMultiplyAdd(p,  r, c2);
				p         = Double.FusedMultiplyAdd(p,  r, c1);
				var exp2x = Double.FusedMultiplyAdd(p,  r, 1.0)
				          * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));
				
				return (exp2x - 1.0) / (exp2x + 1.0);
			}
			""";
	}
}
