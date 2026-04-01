using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares MathF.Pow(10,x) / Math.Pow(10,x) (built-in) against three scalar FastExp10 candidates.
///
/// Candidates:
///   FastExp10    – current ConstExpr generator output.
///                  Reduces x via y = x*ln(10), k = round(y/ln2), r = y − k*ln2,
///                  then evaluates exp(r) via a degree-4 Horner poly.
///                  Key FP ops: 2 MULs + 5 FMAs.
///
///   FastExp10V2  – direct base-10 polynomial, Horner form.
///                  Reduces via k = round(x*log₂10), r = x − k*log₁₀2 in a single step,
///                  eliminating the x*LN10 multiplication entirely.
///                  Polynomial coefficients cₙ = ln(10)ⁿ/n! evaluate 10^r directly.
///                  Key FP ops: 1 MUL + 5 FMAs  (saves 1 MUL vs FastExp10).
///                  Also has better reduction accuracy: FMA acts on original x, not pre-rounded y.
///
///   FastExp10V3  – Estrin's scheme on the degree-4 polynomial (using V2 reduction).
///                  Groups p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
///                  FP critical-path depth shrinks from 4 sequential FMAs (Horner)
///                  to 1 MUL + 3 FMAs — better ILP on out-of-order cores.
///
/// Accuracy note:
///   V2/V3 reduce r to x − k*log₁₀(2) ∈ [−0.151, 0.151].  At the extremes,
///   r*ln(10) ≈ ±0.347 — the same natural-log range as current FastExp10's r.
///   Degree-4 Taylor relative error: ~4e-5 for float and double (fast-math trade-off).
///
/// Polynomial constants (double precision):
///   log₂(10)     = 3.321928094887362
///   log₁₀(2)     = 0.3010299956639812
///   c1 = ln(10)      = 2.302585092994046
///   c2 = ln(10)²/2   = 2.650949055239200
///   c3 = ln(10)³/6   = 2.034678592293477
///   c4 = ln(10)⁴/24  = 1.171255148912267
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method               Category  Mean      Ratio    Note
///   -------------------  --------  --------  ------   ------------------------------------
///   DotNetExp10_Double   Double    4.866 ns  1.00x    built-in, IEEE-accurate
///   FastExp10_Double     Double    1.354 ns  0.28x    current: 2 MULs + 5 FMAs
///   FastExp10V2_Double   Double    1.029 ns  0.21x  ← new: 1 MUL + 5 FMAs, ~24 % faster
///   FastExp10V3_Double   Double    1.080 ns  0.22x    Estrin — slower than V2 on ARM64
///
///   DotNetExp10_Float    Float     2.525 ns  1.00x    built-in
///   FastExp10_Float      Float     1.391 ns  0.55x    current
///   FastExp10V2_Float    Float     1.030 ns  0.41x  ← new: ~26 % faster than current
///   FastExp10V3_Float    Float     1.078 ns  0.43x    Estrin — slower than V2 on ARM64
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*Exp10Benchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Exp10Benchmark
{
	// 1 024 values distributed over the normal range of 10^x.
	// Float: [-38, 38]  → 10^38 ≈ 1e38 < float.MaxValue ≈ 3.4e38; k ≤ 126, no overflow branch.
	// Double: [-300, 300] → 10^300 ≈ 1e300 < double.MaxValue ≈ 1.8e308; k ≤ 996, no overflow branch.
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
			_floatData[i]  = (float)(rng.NextDouble() * 76.0 - 38.0);   // uniform in [-38, 38]
			_doubleData[i] = rng.NextDouble() * 600.0 - 300.0;           // uniform in [-300, 300]
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in MathF.Pow(10f, x) — IEEE-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetExp10_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Pow(10f, v);
		return sum;
	}

	/// <summary>
	/// FastExp10(float) — current ConstExpr optimizer output.
	/// Computes y = x*LN10 first, then reduces k = round(y/ln2), r = y - k*ln2.
	/// Cost: 2 MULs + 5 FMAs in the hot path.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp10_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastExp10Float(v);
		return sum;
	}

	/// <summary>
	/// FastExp10V2(float) — direct base-10 polynomial, Horner form.
	/// Single reduction step: k = round(x*log₂10), r = FMA(-k, log₁₀2, x).
	/// Poly coefficients cₙ = ln(10)ⁿ/n! evaluate 10^r directly — no x*LN10 multiply.
	/// Cost: 1 MUL + 5 FMAs (saves 1 MUL vs FastExp10).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp10V2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += DirectPolyExp10Float(v);
		return sum;
	}

	/// <summary>
	/// FastExp10V3(float) — Estrin's scheme on the degree-4 polynomial (V2 reduction).
	/// p(r) = (1 + c1·r) + r²·((c2 + c3·r) + r²·c4).
	/// Critical-path depth: 1 MUL + 3 FMAs vs 4 sequential FMAs in Horner.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExp10V3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += EstrinExp10Float(v);
		return sum;
	}

	// ── double benchmarks ─────────────────────────────────────────────────

	/// <summary>Built-in Math.Pow(10.0, x) — IEEE-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetExp10_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Pow(10.0, v);
		return sum;
	}

	/// <summary>FastExp10(double) — current ConstExpr optimizer output (2 MULs + 5 FMAs).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp10_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastExp10Double(v);
		return sum;
	}

	/// <summary>FastExp10V2(double) — direct base-10 poly, 1 MUL + 5 FMAs.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp10V2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += DirectPolyExp10Double(v);
		return sum;
	}

	/// <summary>FastExp10V3(double) — Estrin's scheme, shorter FP critical path (depth 3 vs 4 FMAs).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExp10V3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += EstrinExp10Double(v);
		return sum;
	}

	// ── current implementation (mirrored from Exp10FunctionOptimizer) ──────

	private static float CurrentFastExp10Float(float x)
	{
		if (float.IsNaN(x)) return float.NaN;
		if (float.IsPositiveInfinity(x)) return float.PositiveInfinity;
		if (float.IsNegativeInfinity(x)) return 0.0f;
		if (x == 0.0f) return 1.0f;

		if (x >= 38.53f) return float.PositiveInfinity;
		if (x <= -38.53f) return 0.0f;

		const float LN10    = 2.302585092994046f;
		const float INV_LN2 = 1.4426950408889634f;
		const float LN2     = 0.6931471805599453f;

		var y  = x * LN10;
		var kf = y * INV_LN2;
		var k  = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
		var r  = MathF.FusedMultiplyAdd(-k, LN2, y);

		var poly = 1.0f / 24.0f;
		poly = MathF.FusedMultiplyAdd(poly, r, 1.0f / 6.0f);
		poly = MathF.FusedMultiplyAdd(poly, r, 0.5f);
		poly = MathF.FusedMultiplyAdd(poly, r, 1.0f);
		var expR = MathF.FusedMultiplyAdd(poly, r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double CurrentFastExp10Double(double x)
	{
		if (double.IsNaN(x)) return double.NaN;
		if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;
		if (double.IsNegativeInfinity(x)) return 0.0;
		if (x == 0.0) return 1.0;

		if (x >= 309.0) return double.PositiveInfinity;
		if (x <= -309.0) return 0.0;

		const double LN10    = 2.3025850929940456840179914546843642;
		const double INV_LN2 = 1.4426950408889634073599246810018921;
		const double LN2     = 0.6931471805599453094172321214581766;

		var y  = x * LN10;
		var kf = y * INV_LN2;
		var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
		var r  = Math.FusedMultiplyAdd(-k, LN2, y);

		var poly = 1.0 / 24.0;
		poly = Math.FusedMultiplyAdd(poly, r, 1.0 / 6.0);
		poly = Math.FusedMultiplyAdd(poly, r, 0.5);
		poly = Math.FusedMultiplyAdd(poly, r, 1.0);
		var expR = Math.FusedMultiplyAdd(poly, r, 1.0);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}

	// ── V2: direct 10^r polynomial, Horner form ────────────────────────────
	// Reduction: k = round(x·log₂10), r = x − k·log₁₀2  →  |r| ≤ log₁₀2/2 ≈ 0.151.
	// Polynomial:  10^r = Σ_{n=0}^{4} (ln10)ⁿ/n! · rⁿ  — no intermediate x·LN10 multiply.
	// Float max rel. error ≈ 4e-5 (degree-4 Taylor, r·ln10 ∈ [-0.347, 0.347]).

	private static float DirectPolyExp10Float(float x)
	{
		if (x >= 38.53f) return float.IsNaN(x) ? float.NaN : float.PositiveInfinity;
		if (x < -38.53f) return 0.0f;

		const float LOG2_10 = 3.321928094887362f;   // log₂(10)
		const float LOG10_2 = 0.30102999566398120f;  // log₁₀(2) = 1/log₂(10)

		var kf = x * LOG2_10;
		var k  = (int)(kf + (kf >= 0f ? 0.5f : -0.5f));
		var r  = MathF.FusedMultiplyAdd(-k, LOG10_2, x); // r = x − k·log₁₀(2)

		// Degree-4 Horner evaluation of 10^r: cₙ = ln(10)ⁿ / n!
		const float c4 = 1.1712551f;  // ln(10)⁴ / 24
		const float c3 = 2.0346786f;  // ln(10)³ / 6
		const float c2 = 2.6509491f;  // ln(10)² / 2
		const float c1 = 2.3025851f;  // ln(10)

		var p    = MathF.FusedMultiplyAdd(c4, r, c3);
		p        = MathF.FusedMultiplyAdd(p,  r, c2);
		p        = MathF.FusedMultiplyAdd(p,  r, c1);
		var expR = MathF.FusedMultiplyAdd(p,  r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double DirectPolyExp10Double(double x)
	{
		if (x >= 309.0) return double.IsNaN(x) ? double.NaN : double.PositiveInfinity;
		if (x < -309.0) return 0.0;

		const double LOG2_10 = 3.321928094887362347870319429489390;
		const double LOG10_2 = 0.30102999566398119521373889472449303;

		var kf = x * LOG2_10;
		var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
		var r  = Math.FusedMultiplyAdd(-k, LOG10_2, x);

		// Degree-4 Horner evaluation of 10^r: cₙ = ln(10)ⁿ / n!
		const double c4 = 1.1712551489122673;  // ln(10)⁴ / 24
		const double c3 = 2.0346785922934770;  // ln(10)³ / 6
		const double c2 = 2.6509490552391997;  // ln(10)² / 2
		const double c1 = 2.302585092994046;   // ln(10)

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
	//   lo  = FMA(c1, r,  1)       ─┐ both independent of each other and of r²
	//   mid = FMA(c3, r,  c2)      ─┘
	//   r²  = r * r                  ─ computes in parallel with lo, mid
	//   hi  = FMA(c4, r², mid)       ─ depends on r² and mid
	//   expR = FMA(r², hi, lo)       ─ depends on hi and lo
	//
	// FP critical path: r² → hi → expR  =  1 MUL + 2 FMAs  (depth 3)
	// vs Horner critical path: 4 sequential FMAs.

	private static float EstrinExp10Float(float x)
	{
		if (x >= 38.53f) return float.IsNaN(x) ? float.NaN : float.PositiveInfinity;
		if (x < -38.53f) return 0.0f;

		const float LOG2_10 = 3.321928094887362f;
		const float LOG10_2 = 0.30102999566398120f;

		var kf = x * LOG2_10;
		var k  = (int)(kf + (kf >= 0f ? 0.5f : -0.5f));
		var r  = MathF.FusedMultiplyAdd(-k, LOG10_2, x);

		const float c4 = 1.1712551f;
		const float c3 = 2.0346786f;
		const float c2 = 2.6509491f;
		const float c1 = 2.3025851f;

		var r2   = r * r;
		var lo   = MathF.FusedMultiplyAdd(c1, r,  1.0f); // 1 + c1·r
		var mid  = MathF.FusedMultiplyAdd(c3, r,  c2);   // c2 + c3·r
		var hi   = MathF.FusedMultiplyAdd(c4, r2, mid);  // c2 + c3·r + c4·r²
		var expR = MathF.FusedMultiplyAdd(r2, hi, lo);   // lo + r²·hi

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double EstrinExp10Double(double x)
	{
		if (x >= 309.0) return double.IsNaN(x) ? double.NaN : double.PositiveInfinity;
		if (x < -309.0) return 0.0;

		const double LOG2_10 = 3.321928094887362347870319429489390;
		const double LOG10_2 = 0.30102999566398119521373889472449303;

		var kf = x * LOG2_10;
		var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
		var r  = Math.FusedMultiplyAdd(-k, LOG10_2, x);

		const double c4 = 1.1712551489122673;
		const double c3 = 2.0346785922934770;
		const double c2 = 2.6509490552391997;
		const double c1 = 2.302585092994046;

		var r2   = r * r;
		var lo   = Math.FusedMultiplyAdd(c1, r,  1.0);
		var mid  = Math.FusedMultiplyAdd(c3, r,  c2);
		var hi   = Math.FusedMultiplyAdd(c4, r2, mid);
		var expR = Math.FusedMultiplyAdd(r2, hi, lo);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}
}


