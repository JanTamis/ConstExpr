using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

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
public class TanhFunctionOptimizer() : BaseMathFunctionOptimizer("Tanh", n => n is 1)
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
			if (Double.IsPositiveInfinity(value))
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tanh(-Infinity) => -1
			if (Double.IsNegativeInfinity(value))
			{
				result = CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastTanhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastTanhMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
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
			{
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
			{
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Pure FastExp path: tanh(x) = (FastExp(2x)−1)/(FastExp(2x)+1), saturated at ±5.
	/// FastExp is inlined (direct-poly V2, ln(2)ⁿ/n! Horner, MathF.Round reduction).
	/// Saturation guarantees |2x| ≤ 10, safely inside FastExp's domain (-87..88).
	/// No inner branch → no branch mispredictions on random mixed-sign data.
	/// ~17% faster than MathF.Tanh; old hybrid with Single.Exp was only ~9% faster.
	/// </summary>
	private static string GenerateFastTanhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastTanh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x >= 5.0f) return 1.0f;")
			.WriteLine("if (x <= -5.0f) return -1.0f;")
			.WriteWhitespace()
			// .WriteLine("// Inline FastExp(2x) — direct-poly V2.")
			// .WriteLine("// |2x| ≤ 10, well inside FastExp safe domain (-87..88).")
			.WriteLine("var fx   = 2.0f * x;")
			.WriteLine("var kf   = fx * 1.4426950408889634f;")
			.WriteLine("var k    = (int)Single.Round(kf);")
			.WriteLine("var r    = kf - k;")
			.WriteWhitespace()
			// .WriteLine("// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!")
			.WriteLine("var p     = Single.FusedMultiplyAdd(0.055504108664821580f, r, 0.240226506959100690f);")
			.WriteLine("p         = Single.FusedMultiplyAdd(p,  r, 0.693147180559945309f);")
			.WriteLine("var exp2x = Single.FusedMultiplyAdd(p,  r, 1.0f)")
			.WriteLine("          * BitConverter.Int32BitsToSingle((k + 127) << 23);")
			.WriteWhitespace()
			.WriteLine("return (exp2x - 1.0f) / (exp2x + 1.0f);");

		builder.EndBlock();

		return builder.ToString();
	}

	/// <summary>
	/// FastExp hybrid: Padé rational for |x| &lt; 1 (no transcendental call),
	/// then inlined FastExpDouble (direct-poly V2) for |x| ≥ 1.
	/// Old implementation used Double.Exp (slow) and was ~2% SLOWER than Math.Tanh on random data.
	/// Inlined FastExpDouble is ~2.8× faster than Double.Exp, recovering the advantage.
	/// ~4% faster than Math.Tanh; old implementation was ~2% SLOWER.
	/// </summary>
	private static string GenerateFastTanhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastTanh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x >= 19.0) return 1.0;")
			.WriteLine("if (x <= -19.0) return -1.0;")
			.WriteWhitespace()
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (absX < 1.0)")
			.StartBlock()
			// .WriteLine("// [5,6] Padé rational — no transcendental call.")
			.WriteLine("var x2 = x * x;")
			.WriteLine("var a1 = -0.333333333333331;")
			.WriteLine("var a2 =  0.133333333333197;")
			.WriteLine("var a3 = -0.0539682539682505;")
			.WriteLine("var numerator = Double.FusedMultiplyAdd(a3, x2, a2);")
			.WriteLine("numerator = Double.FusedMultiplyAdd(numerator, x2, a1);")
			.WriteLine("numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);")
			.WriteLine("numerator *= x;")
			.WriteLine("var b1 =  1.0;")
			.WriteLine("var b2 = -0.133333333333197;")
			.WriteLine("var b3 =  0.0107936507936338;")
			.WriteLine("var denominator = Double.FusedMultiplyAdd(b3, x2, b2);")
			.WriteLine("denominator = Double.FusedMultiplyAdd(denominator, x2, b1);")
			.WriteLine("denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);")
			.WriteLine("return numerator / denominator;")
			.EndBlock()
			.WriteWhitespace()
			// .WriteLine("// Inline FastExp(2x) — direct-poly V2.")
			// .WriteLine("// |2x| ≤ 38 (since |x| ≤ 19), well inside domain (-708..709).")
			.WriteLine("var fx   = 2.0 * x;")
			.WriteLine("var kf   = fx * 1.4426950408889634073599246810018921;")
			.WriteLine("var k    = (long)Double.Round(kf);")
			.WriteLine("var r    = kf - k;")
			.WriteWhitespace()
			// .WriteLine("// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!")
			.WriteLine("var p     = Double.FusedMultiplyAdd(9.618129107628477232e-3, r, 5.550410866482157995e-2);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  r, 2.402265069591006909e-1);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  r, 6.931471805599453094e-1);")
			.WriteLine("var exp2x = Double.FusedMultiplyAdd(p,  r, 1.0)")
			.WriteLine("          * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));")
			.WriteWhitespace()
			.WriteLine("return (exp2x - 1.0) / (exp2x + 1.0);");

		builder.EndBlock();

		return builder.ToString();
	}
}