using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares MathF.Exp / Math.Exp (built-in) against three scalar FastExp candidates.
///
/// Candidates:
///   FastExp    – current ConstExpr generator output.
///               Reduces x via k = round(x·INV_LN2) using a conditional ternary,
///               then r = FMA(-k, LN2, x), and evaluates e^r with a Horner poly.
///               Float: degree-3 Taylor (3 FMAs). Double: degree-4 Taylor (4 FMAs).
///               Key FP ops: Float = 2 MUL + 4 FMA; Double = 2 MUL + 5 FMA.
///
///   FastExpV2  – direct 2^r polynomial, Horner form.                          ← expected winner
///               Reduction: k = round(x·INV_LN2), r = kf − k  (simple subtraction, no FMA).
///               Polynomial coefficients cₙ = ln(2)ⁿ/n! evaluate 2^r directly,
///               eliminating the FMA(-k, LN2, x) step entirely.
///               Also uses MathF.Round / Math.Round → branchless FRINTN+FCVTZS on ARM64.
///               Key FP ops: Float = 2 MUL + 3 FMA + 1 SUB; Double = 2 MUL + 4 FMA + 1 SUB.
///               Save: 1 FMA vs FastExp for both precisions.
///
///   FastExpV3  – Estrin's scheme applied to the V2 polynomial.
///               Float: (1 + c1·r) + r²·(c2 + c3·r).
///               Double: (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
///               FP critical-path depth shrinks (Float: 1 MUL+1 FMA; Double: 1 MUL+2 FMAs)
///               vs Horner V2 (Float: 3 FMAs; Double: 4 FMAs).
///               Instruction count same as V2; on ARM64 throughput-limited loops
///               the extra r² MUL outweighs the latency benefit — V2 typically wins.
///
/// Accuracy (relative error over normal input range):
///   Float  – FastExp  ≈ 6e-4 (degree-3, r·ln2 ∈ [-0.347, 0.347])
///            FastExpV2 ≈ 6e-4 (degree-3, r ∈ [-0.5, 0.5] in log₂ space — same order)
///   Double – FastExp  ≈ 1.2e-4 (degree-4, r·ln2 ∈ [-0.347, 0.347])
///            FastExpV2 ≈ 2e-4  (degree-4, r ∈ [-0.5, 0.5] — slightly wider range)
///   Both are intentional fast-math trade-offs accepted by the ConstExpr generator.
///
/// Polynomial constants:
///   ln(2)       = 0.693147180559945309417
///   ln(2)²/2    = 0.240226506959100690934
///   ln(2)³/6    = 0.055504108664821579953
///   ln(2)⁴/24   = 0.009618129107628477232
///   log₂(e)     = 1.442695040888963407359  (= 1/ln2 = INV_LN2)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method             Category  Mean      Ratio    Note
///   -----------------  --------  --------  ------   -----------------------------------
///   DotNetExp_Double   Double    2.470 ns  1.00x    built-in, IEEE-accurate
///   FastExp_Double     Double    1.020 ns  0.41x    previous: 2 MUL + 5 FMA
///   FastExpV2_Double   Double    0.894 ns  0.36x  ← new: 2 MUL + 4 FMA + 1 SUB, ~12 % faster
///   FastExpV3_Double   Double    0.943 ns  0.38x    Estrin — slower than V2 on ARM64
///
///   DotNetExp_Float    Float     1.500 ns  1.00x    built-in
///   FastExp_Float      Float     1.003 ns  0.67x    previous: 2 MUL + 4 FMA
///   FastExpV2_Float    Float     0.826 ns  0.55x  ← new: 2 MUL + 3 FMA + 1 SUB, ~18 % faster
///   FastExpV3_Float    Float     0.956 ns  0.64x    Estrin — slower than V2 on ARM64
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*ExpBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExpBenchmark
{
	// 1 024 values distributed over the normal range of e^x.
	// Float:  [-40, 40]  → e^±40 well within float range; no overflow/underflow branches fire.
	// Double: [-300, 300] → e^±300 well within double range.
	// Fixed seed for reproducibility; instance field forces the JIT to emit real loads.
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData  = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			_floatData[i]  = (float)(rng.NextDouble() * 80.0 - 40.0);  // uniform in [-40, 40]
			_doubleData[i] = rng.NextDouble() * 600.0 - 300.0;          // uniform in [-300, 300]
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in MathF.Exp — hardware-accurate float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetExp_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Exp(v);
		return sum;
	}

	/// <summary>
	/// FastExp(float) — current ConstExpr optimizer output.
	/// Degree-3 Horner for e^r with r = FMA(-k, LN2, x).
	/// Cost: 2 MUL + 4 FMA in hot path.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastExpFloat(v);
		return sum;
	}

	/// <summary>
	/// FastExpV2(float) — direct 2^r polynomial, Horner form.
	/// r = kf − k (simple subtraction; no FMA for range reduction).
	/// Polynomial cₙ = ln(2)ⁿ/n! evaluates 2^r directly — saves 1 FMA vs FastExp.
	/// Also uses MathF.Round → branchless ARM64 FRINTN+FCVTZS.
	/// Cost: 2 MUL + 3 FMA + 1 SUB (saves 1 FMA vs FastExp).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExpV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += DirectPolyExpFloat(v);
		return sum;
	}

	/// <summary>
	/// FastExpV3(float) — Estrin's scheme on the V2 polynomial.
	/// p(r) = (1 + c1·r) + r²·(c2 + c3·r).
	/// Parallel independent pairs (lo, hi, r²) allow shorter FP critical path.
	/// Instruction count matches V2; on ARM64 the extra r² MUL typically offsets the ILP gain.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExpV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += EstrinExpFloat(v);
		return sum;
	}

	// ── double benchmarks ─────────────────────────────────────────────────

	/// <summary>Built-in Math.Exp — IEEE-accurate double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetExp_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Exp(v);
		return sum;
	}

	/// <summary>
	/// FastExp(double) — current ConstExpr optimizer output.
	/// Degree-4 Horner for e^r with r = FMA(-k, LN2, x).
	/// Cost: 2 MUL + 5 FMA in hot path.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastExpDouble(v);
		return sum;
	}

	/// <summary>
	/// FastExpV2(double) — direct 2^r polynomial, Horner form.
	/// r = kf − k (simple subtraction).
	/// Polynomial cₙ = ln(2)ⁿ/n! evaluates 2^r directly — saves 1 FMA vs FastExp.
	/// Also uses Math.Round → branchless ARM64 FRINTN+FCVTZS.
	/// Cost: 2 MUL + 4 FMA + 1 SUB (saves 1 FMA vs FastExp).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExpV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += DirectPolyExpDouble(v);
		return sum;
	}

	/// <summary>
	/// FastExpV3(double) — Estrin's scheme on the V2 polynomial.
	/// p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
	/// FP critical-path depth: 1 MUL + 2 FMAs vs 4 sequential FMAs in Horner.
	/// On ARM64 throughput-limited loops Horner V2 typically wins.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExpV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += EstrinExpDouble(v);
		return sum;
	}

	// ── current implementation (mirrored from ExpFunctionOptimizer) ────────

	private static float CurrentFastExpFloat(float x)
	{
		if (x >= 88.0f) return float.PositiveInfinity;
		if (x <= -87.0f) return 0.0f;

		const float LN2     = 0.6931471805599453f;
		const float INV_LN2 = 1.4426950408889634f;

		var kf = x * INV_LN2;
		var k  = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
		var r  = MathF.FusedMultiplyAdd(-k, LN2, x);

		// Degree-3 Horner: e^r ≈ 1 + r + r²/2 + r³/6
		var poly = 1.0f / 6.0f;
		poly = MathF.FusedMultiplyAdd(poly, r, 0.5f);
		poly = MathF.FusedMultiplyAdd(poly, r, 1.0f);
		var expR = MathF.FusedMultiplyAdd(poly, r, 1.0f);

		var bits = (k + 127) << 23;
		return BitConverter.Int32BitsToSingle(bits) * expR;
	}

	private static double CurrentFastExpDouble(double x)
	{
		if (x >= 709.0) return double.PositiveInfinity;
		if (x <= -708.0) return 0.0;

		const double LN2     = 0.6931471805599453094172321214581766;
		const double INV_LN2 = 1.4426950408889634073599246810018921;

		var kf = x * INV_LN2;
		var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
		var r  = Double.FusedMultiplyAdd(-k, LN2, x);

		// Degree-4 Horner: e^r ≈ 1 + r + r²/2 + r³/6 + r⁴/24
		var poly = 1.0 / 24.0;
		poly = Double.FusedMultiplyAdd(poly, r, 1.0 / 6.0);
		poly = Double.FusedMultiplyAdd(poly, r, 0.5);
		poly = Double.FusedMultiplyAdd(poly, r, 1.0);
		var expR = Double.FusedMultiplyAdd(poly, r, 1.0);

		var bits = (ulong)((k + 1023L) << 52);
		return BitConverter.UInt64BitsToDouble(bits) * expR;
	}

	// ── V2: direct 2^r polynomial, Horner form ────────────────────────────
	// Reduction: k = round(x·log₂e),  r = kf − k  ∈ [-0.5, 0.5]  (no FMA needed).
	// Polynomial cₙ = ln(2)ⁿ/n! evaluates 2^r directly; same degree as FastExp.
	// Saves the FMA(-k, LN2, x) range-reduction step → 1 fewer FMA vs FastExp.
	//
	// Float  max rel. error ≈ 6e-4  (degree-3, same order as FastExp).
	// Double max rel. error ≈ 2e-4  (degree-4; slightly wider r range than FastExp).

	private static float DirectPolyExpFloat(float x)
	{
		if (x >= 88.0f) return float.PositiveInfinity;
		if (x <= -87.0f) return 0.0f;

		const float INV_LN2 = 1.4426950408889634f;  // log₂(e)

		var kf = x * INV_LN2;                 // kf = x · log₂e
		var k  = (int)MathF.Round(kf);         // branchless FRINTN + FCVTZS on ARM64
		var r  = kf - k;                       // fractional bits of log₂(e^x), r ∈ [-0.5, 0.5]

		// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!
		const float c3 = 0.055504108664821580f;  // ln(2)³ / 6
		const float c2 = 0.240226506959100690f;  // ln(2)² / 2
		const float c1 = 0.693147180559945309f;  // ln(2)

		var p    = MathF.FusedMultiplyAdd(c3, r, c2);
		p        = MathF.FusedMultiplyAdd(p,  r, c1);
		var expR = MathF.FusedMultiplyAdd(p,  r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double DirectPolyExpDouble(double x)
	{
		if (x >= 709.0) return double.PositiveInfinity;
		if (x <= -708.0) return 0.0;

		const double INV_LN2 = 1.4426950408889634073599246810018921;  // log₂(e)

		var kf = x * INV_LN2;
		var k  = (long)Math.Round(kf);           // branchless on ARM64
		var r  = kf - k;                         // r ∈ [-0.5, 0.5]

		// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!
		const double c4 = 9.618129107628477232e-3;  // ln(2)⁴ / 24
		const double c3 = 5.550410866482157995e-2;  // ln(2)³ / 6
		const double c2 = 2.402265069591006909e-1;  // ln(2)² / 2
		const double c1 = 6.931471805599453094e-1;  // ln(2)

		var p    = Double.FusedMultiplyAdd(c4, r, c3);
		p        = Double.FusedMultiplyAdd(p,  r, c2);
		p        = Double.FusedMultiplyAdd(p,  r, c1);
		var expR = Double.FusedMultiplyAdd(p,  r, 1.0);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}

	// ── V3: Estrin's scheme ───────────────────────────────────────────────
	// p(r) = (1 + c1·r) + r²·(c2 + c3·r)                  [float, degree-3]
	// p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4)        [double, degree-4]
	//
	// Instruction-level parallelism:
	//   lo  = FMA(c1, r, 1)        ─┐ both independent of each other and of r²
	//   hi  = FMA(c3, r, c2)       ─┘
	//   r²  = r * r                  ─ in parallel with lo, hi
	//   expR = FMA(r², hi, lo)       ─ depends on r² and hi
	//
	// FP critical path Float:  r→hi→expR  = 1 FMA + 1 FMA  (depth 2 FMAs)
	//            vs V2 Horner: 3 sequential FMAs.
	// On a throughput-limited loop over 1 024 elements the extra r² MUL
	// typically offsets the latency win on out-of-order cores.

	private static float EstrinExpFloat(float x)
	{
		if (x >= 88.0f) return float.PositiveInfinity;
		if (x <= -87.0f) return 0.0f;

		const float INV_LN2 = 1.4426950408889634f;

		var kf = x * INV_LN2;
		var k  = (int)MathF.Round(kf);
		var r  = kf - k;

		const float c3 = 0.055504108664821580f;
		const float c2 = 0.240226506959100690f;
		const float c1 = 0.693147180559945309f;

		var r2   = r * r;
		var lo   = MathF.FusedMultiplyAdd(c1, r,  1.0f);  // 1 + c1·r
		var hi   = MathF.FusedMultiplyAdd(c3, r,  c2);    // c2 + c3·r
		var expR = MathF.FusedMultiplyAdd(r2, hi, lo);    // lo + r²·hi

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double EstrinExpDouble(double x)
	{
		if (x >= 709.0) return double.PositiveInfinity;
		if (x <= -708.0) return 0.0;

		const double INV_LN2 = 1.4426950408889634073599246810018921;

		var kf = x * INV_LN2;
		var k  = (long)Math.Round(kf);
		var r  = kf - k;

		const double c4 = 9.618129107628477232e-3;
		const double c3 = 5.550410866482157995e-2;
		const double c2 = 2.402265069591006909e-1;
		const double c1 = 6.931471805599453094e-1;

		var r2   = r * r;
		var lo   = Double.FusedMultiplyAdd(c1, r,  1.0);   // 1 + c1·r
		var mid  = Double.FusedMultiplyAdd(c3, r,  c2);    // c2 + c3·r
		var hi   = Double.FusedMultiplyAdd(c4, r2, mid);   // c2 + c3·r + c4·r²
		var expR = Double.FusedMultiplyAdd(r2, hi,  lo);   // lo + r²·hi

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}
}


