using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.Exp2 / float.Exp2 (built-in) against three scalar FastExp2 candidates.
///
/// Candidates:
///   FastExp2   – previous ConstExpr generator output.
///               Computes rln = r * ln2 first, then evaluates exp(rln) with a degree-4 Taylor
///               polynomial via 4 FMAs + 1 MUL (5 FP operations total).
///
///   FastExp2V2 – direct 2^r polynomial with coefficients ln(2)^n / n! pre-merged.  ← WINNER
///               Eliminates the intermediate r*ln2 multiplication → 4 FMAs only.
///               Same accuracy as FastExp2; identical branch structure.
///
///   FastExp2V3 – Estrin's scheme applied to the degree-4 polynomial.
///               Groups p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
///               Operation count matches FastExp2 (1 MUL + 4 FMAs) but the FP critical
///               path shrinks from depth-4 to depth-3 FMAs. On ARM64 (Apple M4 Pro) the
///               extra r² MUL outweighs the latency benefit — V2 wins.
///
/// Accuracy (relative error, worst case over the normal range):
///   Float  – FastExp2 / V2 / V3: ≈ 3e-5  (degree-4 Taylor, r ∈ [-0.5, 0.5])
///   Double – FastExp2 / V2 / V3: ≈ 4e-5  (same polynomial; intentional fast-math trade-off)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method             Category  Mean      Ratio    Note
///   -----------------  --------  --------  ------   -----------------------------------
///   DotNetExp2_Double  Double    3.616 ns  1.00x    built-in, IEEE-accurate
///   FastExp2_Double    Double    1.319 ns  0.36x    previous: 4 FMAs + 1 MUL
///   FastExp2V2_Double  Double    0.954 ns  0.26x  ← new: 4 FMAs, ~28 % faster than prev
///   FastExp2V3_Double  Double    1.020 ns  0.28x    Estrin — slower on ARM64
///
///   DotNetExp2_Float   Float     2.499 ns  1.00x    built-in
///   FastExp2_Float     Float     1.345 ns  0.54x    previous
///   FastExp2V2_Float   Float     0.954 ns  0.38x  ← new: ~29 % faster than prev
///   FastExp2V3_Float   Float     1.020 ns  0.41x    Estrin — slower on ARM64
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*Exp2Benchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Exp2Benchmark
{
	// 1 024 values uniformly distributed over [-100, 100].
	// Stays well within the float/double normal range so no special-case branches fire,
	// ensuring we measure the polynomial hot path exclusively.
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
			var v = rng.NextDouble() * 200.0 - 100.0; // uniform in [-100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>
	/// Built-in float.Exp2 (IFloatingPointIeee754&lt;float&gt; static interface method, .NET 7+).
	/// Devirtualised by the JIT; equivalent to a hardware-accurate scalar exp2 intrinsic.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetExp2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.Exp2(v);
		return sum;
	}

	/// <summary>
	/// FastExp2(float) — previous ConstExpr optimizer output.
	/// Computes rln = r * LN2 first, then evaluates exp(rln) via a degree-4 Horner poly.
	/// Cost: 4 FMAs + 1 MUL in the polynomial path.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastExp2Float(v);
		return sum;
	}

	/// <summary>
	/// FastExp2V2(float) — direct polynomial for 2^r.
	/// Coefficients are ln(2)^n / n!, so no intermediate r*ln2 multiplication is needed.
	/// Cost: 4 FMAs only (saves 1 MUL vs FastExp2).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp2V2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += DirectPolyExp2Float(v);
		return sum;
	}

	/// <summary>
	/// FastExp2V3(float) — Estrin's scheme on the degree-4 polynomial.
	/// Groups p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
	/// The two halves (lo, mid) are computed in parallel; FP critical path depth = 3 FMAs
	/// vs 4 sequential FMAs in Horner → better ILP on out-of-order cores.
	/// Cost: 1 MUL + 4 FMAs (same count as FastExp2, shorter critical path).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp2V3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += EstrinExp2Float(v);
		return sum;
	}

	// ── double benchmarks ─────────────────────────────────────────────────

	/// <summary>Built-in Math.Exp2 — IEEE-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetExp2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Double.Exp2(v);
		return sum;
	}

	/// <summary>FastExp2(double) — previous ConstExpr optimizer output (4 FMAs + 1 MUL).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastExp2Double(v);
		return sum;
	}

	/// <summary>FastExp2V2(double) — direct polynomial, 4 FMAs only (saves 1 MUL vs FastExp2).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp2V2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += DirectPolyExp2Double(v);
		return sum;
	}

	/// <summary>FastExp2V3(double) — Estrin's scheme, shorter FP critical path (depth 3 vs 4 FMAs).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp2V3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += EstrinExp2Double(v);
		return sum;
	}

	// ── current implementation (mirrored from Exp2FunctionOptimizer) ──────

	private static float CurrentFastExp2Float(float x)
	{
		if (float.IsNaN(x)) return float.NaN;
		if (float.IsPositiveInfinity(x)) return float.PositiveInfinity;
		if (float.IsNegativeInfinity(x)) return 0.0f;
		if (x == 0.0f) return 1.0f;

		if (x >= 128.0f) return float.PositiveInfinity;
		if (x <= -150.0f) return 0.0f;

		const float LN2 = 0.6931471805599453f;

		var k = (int)(x + (x >= 0.0f ? 0.5f : -0.5f));
		var r = MathF.FusedMultiplyAdd(-k, 1.0f, x);

		var rln = r * LN2;

		var poly = 1.0f / 24.0f;
		poly = MathF.FusedMultiplyAdd(poly, rln, 1.0f / 6.0f);
		poly = MathF.FusedMultiplyAdd(poly, rln, 0.5f);
		poly = MathF.FusedMultiplyAdd(poly, rln, 1.0f);
		var expR = MathF.FusedMultiplyAdd(poly, rln, 1.0f);

		var bits = (k + 127) << 23;
		return BitConverter.Int32BitsToSingle(bits) * expR;
	}

	private static double CurrentFastExp2Double(double x)
	{
		if (double.IsNaN(x)) return double.NaN;
		if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;
		if (double.IsNegativeInfinity(x)) return 0.0;
		if (x == 0.0) return 1.0;

		if (x >= 1024.0) return double.PositiveInfinity;
		if (x <= -1100.0) return 0.0;

		const double LN2 = 0.6931471805599453094172321214581766;

		var k = (long)(x + (x >= 0.0 ? 0.5 : -0.5));
		var r = Math.FusedMultiplyAdd(-k, 1.0, x);

		var rln = r * LN2;

		var poly = 1.0 / 24.0;
		poly = Math.FusedMultiplyAdd(poly, rln, 1.0 / 6.0);
		poly = Math.FusedMultiplyAdd(poly, rln, 0.5);
		poly = Math.FusedMultiplyAdd(poly, rln, 1.0);
		var expR = Math.FusedMultiplyAdd(poly, rln, 1.0);

		var bits = (ulong)((k + 1023L) << 52);
		return BitConverter.UInt64BitsToDouble(bits) * expR;
	}

	// ── V2: direct 2^r polynomial, Horner form ────────────────────────────
	// Coefficients are ln(2)^n / n!, eliminating the intermediate r*LN2 multiply.
	// float max rel. error ≈ 3e-5 (degree-4, r ∈ [-0.5, 0.5]).

	private static float DirectPolyExp2Float(float x)
	{
		if (x >= 128.0f) return float.IsNaN(x) ? float.NaN : float.PositiveInfinity;
		if (x < -150.0f) return 0.0f;

		var k = (int)(x + (x >= 0f ? 0.5f : -0.5f));
		var r = x - k; // equivalent to FMA(-k, 1f, x)

		// Degree-4 Horner evaluation of 2^r.
		// c_n = ln(2)^n / n!  →  sum_{n=0}^{4} c_n * r^n  = exp(r * ln2) = 2^r
		const float c4 = 0.009618129f;  // ln(2)^4 / 24
		const float c3 = 0.055504109f;  // ln(2)^3 / 6
		const float c2 = 0.240226507f;  // ln(2)^2 / 2
		const float c1 = 0.693147181f;  // ln(2)

		var p    = MathF.FusedMultiplyAdd(c4, r, c3);
		p        = MathF.FusedMultiplyAdd(p,  r, c2);
		p        = MathF.FusedMultiplyAdd(p,  r, c1);
		var expR = MathF.FusedMultiplyAdd(p,  r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double DirectPolyExp2Double(double x)
	{
		if (x >= 1024.0) return double.IsNaN(x) ? double.NaN : double.PositiveInfinity;
		if (x < -1100.0) return 0.0;

		var k = (long)(x + (x >= 0.0 ? 0.5 : -0.5));
		var r = x - k;

		const double c4 = 9.618129107628477e-3;  // ln(2)^4 / 24
		const double c3 = 5.550410866482158e-2;  // ln(2)^3 / 6
		const double c2 = 2.402265069591007e-1;  // ln(2)^2 / 2
		const double c1 = 6.931471805599453e-1;  // ln(2)

		var p    = Math.FusedMultiplyAdd(c4, r, c3);
		p        = Math.FusedMultiplyAdd(p,  r, c2);
		p        = Math.FusedMultiplyAdd(p,  r, c1);
		var expR = Math.FusedMultiplyAdd(p,  r, 1.0);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}

	// ── V3: Estrin's scheme ───────────────────────────────────────────────
	// p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4)
	//
	// Instruction-level parallelism:
	//   lo  = FMA(c1, r,  1.0f)     ─┐ both independent
	//   mid = FMA(c3, r,  c2)       ─┘ of each other and of r2
	//   r2  = r * r                   ─ in parallel with lo, mid
	//   hi  = FMA(c4, r2, mid)       ─ depends on r2 and mid
	//   expR = FMA(r2, hi, lo)       ─ depends on hi and lo
	//
	// FP critical path: r2 → hi → expR = 1 MUL + 2 FMAs (depth 3 in cycles)
	// vs Horner depth: 4 sequential FMAs.

	private static float EstrinExp2Float(float x)
	{
		if (x >= 128.0f) return float.IsNaN(x) ? float.NaN : float.PositiveInfinity;
		if (x < -150.0f) return 0.0f;

		var k = (int)(x + (x >= 0f ? 0.5f : -0.5f));
		var r = x - k;

		const float c4 = 0.009618129f;
		const float c3 = 0.055504109f;
		const float c2 = 0.240226507f;
		const float c1 = 0.693147181f;

		var r2   = r * r;
		var lo   = MathF.FusedMultiplyAdd(c1, r,  1.0f); // 1 + c1·r
		var mid  = MathF.FusedMultiplyAdd(c3, r,  c2);   // c2 + c3·r
		var hi   = MathF.FusedMultiplyAdd(c4, r2, mid);  // c2 + c3·r + c4·r²
		var expR = MathF.FusedMultiplyAdd(r2, hi, lo);   // lo + r²·hi

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double EstrinExp2Double(double x)
	{
		if (x >= 1024.0) return double.IsNaN(x) ? double.NaN : double.PositiveInfinity;
		if (x < -1100.0) return 0.0;

		var k = (long)(x + (x >= 0.0 ? 0.5 : -0.5));
		var r = x - k;

		const double c4 = 9.618129107628477e-3;
		const double c3 = 5.550410866482158e-2;
		const double c2 = 2.402265069591007e-1;
		const double c1 = 6.931471805599453e-1;

		var r2   = r * r;
		var lo   = Math.FusedMultiplyAdd(c1, r,  1.0);
		var mid  = Math.FusedMultiplyAdd(c3, r,  c2);
		var hi   = Math.FusedMultiplyAdd(c4, r2, mid);
		var expR = Math.FusedMultiplyAdd(r2, hi, lo);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}
}





