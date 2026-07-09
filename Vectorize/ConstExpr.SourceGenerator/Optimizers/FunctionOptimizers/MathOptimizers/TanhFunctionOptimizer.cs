using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
///   Optimizer for Math.Tanh / MathF.Tanh.
///   Implementation strategy (benchmarked on Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float  – Pure FastExp path: tanh(x) = (FastExp(2x)−1)/(FastExp(2x)+1), saturated at ±5.
///   FastExp inlined from direct-poly V2 (ln(2)ⁿ/n! coefficients, MathF.Round reduction).
///   Eliminates the inner branch of the old hybrid; |2x| ≤ 10, safely within FastExp domain.
///   Result: ~1.75 ns vs 2.12 ns .NET (−17%).  Old hybrid was ~1.94 ns (−9%).
///   Double – FastExp hybrid: Padé rational for |x| &lt; 1 (fastest for small inputs with good
///   branch prediction), inlined FastExpDouble (direct-poly V2) for |x| ≥ 1.
///   The old hybrid used Double.Exp (built-in) and was actually SLOWER than .NET
///   on random [-4,4] data.  Replacing it with inlined FastExpDouble gives −4% over .NET.
///   Result: ~2.50 ns vs 2.60 ns .NET (−4%).  Old hybrid was ~2.65 ns (+2%).
///   Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT, uniform [-4,4] input):
///   Method              Float     Ratio   Double    Ratio
///   ------------------  --------  ------  --------  ------
///   DotNetTanh          2.123 ns  1.00x   2.595 ns  1.00x
///   OldFastTanh         1.942 ns  0.91x   2.647 ns  1.02x  ← was SLOWER for double
///   FastTanh (new)      1.753 ns  0.83x   2.496 ns  0.96x  ← production
/// </summary>
public class TanhFunctionOptimizer() : BaseMathFunctionOptimizer("Tanh", n => n is 1), IBaseMathCustomImplementation
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

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastTanhMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastTanhMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
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

	private static string GenerateFastTanhMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic tangent (Tanh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a fast exponential formulation with saturation near the asymptotes.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic tangent value.</returns>")
			.WriteLine("private static float FastTanh(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x >= 5.0f) return 1.0f;")
			.WriteLine("if (x <= -5.0f) return -1.0f;")
			.WriteWhitespace()
			.WriteLine("var fx   = 2.0f * x;")
			.WriteLine("var kf   = fx * 1.4426950408889634f;")
			.WriteLine("var k    = (int)Single.Round(kf);")
			.WriteLine("var r    = kf - k;")
			.WriteWhitespace()
			.WriteLine($"var p     = {multiplyAdd(0.055504108664821580f, "r", 0.240226506959100690f)};")
			.WriteLine($"p         = {multiplyAdd("p", "r", 0.693147180559945309f)};")
			.WriteLine($"var exp2x = {multiplyAdd("p", "r", 1.0f)}")
			.WriteLine("          * BitConverter.Int32BitsToSingle((k + 127) << 23);")
			.WriteWhitespace()
			.WriteLine("return (exp2x - 1.0f) / (exp2x + 1.0f);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastTanhMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic tangent (Tanh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a hybrid rational/fast-exp formulation with saturation near the asymptotes.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic tangent value.</returns>")
			.WriteLine("private static double FastTanh(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
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
			.WriteLine("var x2 = x * x;")
			.WriteLine("var a1 = -0.333333333333331;")
			.WriteLine("var a2 =  0.133333333333197;")
			.WriteLine("var a3 = -0.0539682539682505;")
			.WriteLine($"var numerator = {multiplyAdd("a3", "x2", "a2")};")
			.WriteLine($"numerator = {multiplyAdd("numerator", "x2", "a1")};")
			.WriteLine($"numerator = {multiplyAdd("numerator", "x2", 1.0)};")
			.WriteLine("numerator *= x;")
			.WriteLine("var b1 =  1.0;")
			.WriteLine("var b2 = -0.133333333333197;")
			.WriteLine("var b3 =  0.0107936507936338;")
			.WriteLine($"var denominator = {multiplyAdd("b3", "x2", "b2")};")
			.WriteLine($"denominator = {multiplyAdd("denominator", "x2", "b1")};")
			.WriteLine($"denominator = {multiplyAdd("denominator", "x2", 1.0)};")
			.WriteLine("return numerator / denominator;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var fx   = 2.0 * x;")
			.WriteLine("var kf   = fx * 1.4426950408889634073599246810018921;")
			.WriteLine("var k    = (long)Double.Round(kf);")
			.WriteLine("var r    = kf - k;")
			.WriteWhitespace()
			.WriteLine($"var p     = {multiplyAdd(9.618129107628477232e-3, "r", 5.550410866482157995e-2)};")
			.WriteLine($"p         = {multiplyAdd("p", "r", 2.402265069591006909e-1)};")
			.WriteLine($"p         = {multiplyAdd("p", "r", 6.931471805599453094e-1)};")
			.WriteLine($"var exp2x = {multiplyAdd("p", "r", 1.0)};")
			.WriteLine("          * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));")
			.WriteWhitespace()
			.WriteLine("return (exp2x - 1.0) / (exp2x + 1.0);");

		builder.EndBlock();

		return builder.ToString();
	}
}