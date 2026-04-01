using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares MathF.Log / Math.Log (built-in) against three scalar FastLog candidates.
///
/// Candidates:
///   FastLog    – bit-extraction + degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
///                ln(x) = e·ln(2) + ln(m)  — no LOG10_E scaling step needed (unlike Log10).
///                Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast.
///
///   FastLogV2  – same polynomial evaluated via Estrin's scheme.
///                p(m) = (c0 + c1·m) + m²·(c2 + c3·m) + m⁴·c4.
///                lo  = FMA(c1, m, c0)       ─┐ both independent of each other and of m²
///                hi  = FMA(c3, m, c2)       ─┘
///                m²  = m * m                  ─ in parallel with lo, hi
///                mid = FMA(m², hi, lo)         ─ depends on m² and hi
///                lnm = FMA(m⁴, c4, mid)        ─ depends on m⁴ and mid
///                FP critical-path depth: 3 ops vs 4 sequential FMAs in Horner.
///                Total ops: 3 FMAs + 3 MULs — extra MULs may offset latency gain on ARM64.
///
///   FastLogV3  – via Math.Log2(x) · ln(2).
///                Delegates to the built-in log2 intrinsic then scales.
///                Expected similar throughput to the baseline (still a transcendental call).
///
///   FastLog2Param  – optimizer output for Math.Log(x, newBase): FastLog(x) / FastLog(newBase).
///                    log_base(x) = ln(x) / ln(newBase) — reuses the same Horner helper.
///                    Key FP ops: 2 × (4 FMAs + 1 MUL + 1 bit-cast) + 1 DIV.
///
/// Polynomial constants (degree-4 ln(m) minimax, m ∈ [1, 2)):
///   c0 = -1.741793927   c1 =  2.821202636   c2 = -1.469956800
///   c3 =  0.447178975   c4 = -0.056570851
///   Max relative error ≈ 8.7e-5 (fast-math trade-off).
///
/// Natural log constant:
///   ln(2)  = 0.6931471805599453094172321214581766
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method            Category  Mean      Ratio    Note
///   ----------------  --------  --------  ------   -----------------------------------
///   DotNetLog_Double  Double    2.003 ns  1.00x    built-in, IEEE-accurate
///   FastLog_Double    Double    0.904 ns  0.45x  ← winner: 4 FMAs + 1 MUL, ~2.2× faster
///   FastLogV2_Double  Double    1.005 ns  0.50x    Estrin — extra MULs offset latency gain
///   FastLogV3_Double  Double    2.024 ns  1.01x    via Math.Log2 — same as baseline
///
///   DotNetLog_Float   Float     1.764 ns  1.00x    built-in
///   FastLog_Float     Float     0.888 ns  0.50x  ← winner: 4 FMAs + 1 MUL, ~2.0× faster
///   FastLogV2_Float   Float     0.990 ns  0.56x    Estrin — extra MULs offset latency gain
///   FastLogV3_Float   Float     1.500 ns  0.85x    via MathF.Log2 — hw transcendental
///
///   DotNetLog2Param_Double  TwoParam_Double  4.250 ns  1.00x    built-in Math.Log(x, base)
///   FastLog2Param_Double    TwoParam_Double  2.000 ns  0.47x  ← new: FastLog(x)/FastLog(base), ~2.1× faster
///
///   DotNetLog2Param_Float   TwoParam_Float   4.541 ns  1.00x    built-in Math.Log(x, base)
///   FastLog2Param_Float     TwoParam_Float   2.021 ns  0.45x  ← new: FastLog(x)/FastLog(base), ~2.2× faster
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*LogBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class LogBenchmark
{
	// 1 024 normal positive values spanning a wide exponent range.
	// Float:  10^x for x ∈ [-20, 20]  → values in [1e-20, 1e20], all normal positive floats.
	// Double: 10^x for x ∈ [-100, 100] → values in [1e-100, 1e100], all normal positive doubles.
	// Base values: uniform in [2, 10] — covers realistic logarithm bases, excludes 1 (degenerate).
	// Fixed seed; instance fields force the JIT to emit real loads per iteration.
	private const int N = 1_024;
	private float[]  _floatData      = null!;
	private double[] _doubleData     = null!;
	private float[]  _baseFloatData  = null!;
	private double[] _baseDoubleData = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData      = new float[N];
		_doubleData     = new double[N];
		_baseFloatData  = new float[N];
		_baseDoubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			_floatData[i]      = (float)Math.Pow(10.0, rng.NextDouble() * 40.0 - 20.0);
			_doubleData[i]     = Math.Pow(10.0, rng.NextDouble() * 200.0 - 100.0);
			_baseFloatData[i]  = (float)(rng.NextDouble() * 8.0 + 2.0);  // [2, 10]
			_baseDoubleData[i] = rng.NextDouble() * 8.0 + 2.0;           // [2, 10]
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in MathF.Log — hardware-accurate float result (= current optimizer output).</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetLog_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Log(v);
		return sum;
	}

	/// <summary>
	/// FastLog(float) — bit-extract exponent + degree-4 Horner for ln(m), m ∈ [1, 2).
	/// ln(x) = e·ln(2) + ln(m).  No LOG10_E conversion needed (vs Log10 implementation).
	/// Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLog_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastLogFloat(v);
		return sum;
	}

	/// <summary>
	/// FastLogV2(float) — same polynomial via Estrin's scheme.
	/// p(m) = (c0 + c1·m) + m²·(c2 + c3·m) + m⁴·c4.
	/// FP critical-path depth: 3 ops vs 4 sequential FMAs in Horner.
	/// On ARM64 throughput-limited loops the extra MULs may offset the latency benefit.
	/// Key FP ops: 3 FMAs + 3 MULs + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLogV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastLogV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastLogV3(float) — via MathF.Log2(x) * ln(2).
	/// Delegates to the built-in log2 intrinsic then scales by ln(2).
	/// Expected similar throughput to the baseline (still a hardware transcendental call).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLogV3_Float()
	{
		var sum = 0f;
		const float LN2 = 0.6931471805599453f;
		foreach (var v in _floatData)
			sum += MathF.Log2(v) * LN2;
		return sum;
	}

	// ── double benchmarks ─────────────────────────────────────────────────

	/// <summary>Built-in Math.Log — IEEE-accurate double result (= current optimizer output).</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetLog_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Log(v);
		return sum;
	}

	/// <summary>
	/// FastLog(double) — bit-extract exponent + degree-4 Horner for ln(m), m ∈ [1, 2).
	/// ln(x) = e·ln(2) + ln(m).  No LOG10_E conversion needed (vs Log10 implementation).
	/// Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLog_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastLogDouble(v);
		return sum;
	}

	/// <summary>
	/// FastLogV2(double) — same polynomial via Estrin's scheme.
	/// p(m) = (c0 + c1·m) + m²·(c2 + c3·m) + m⁴·c4.
	/// FP critical-path depth: 3 ops vs 4 sequential FMAs in Horner.
	/// On ARM64 throughput-limited loops the extra MULs may offset the latency benefit.
	/// Key FP ops: 3 FMAs + 3 MULs + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLogV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastLogV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastLogV3(double) — via Math.Log2(x) * ln(2).
	/// Delegates to the built-in log2 intrinsic then scales by ln(2).
	/// Expected similar throughput to the baseline (still a hardware transcendental call).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLogV3_Double()
	{
		var sum = 0.0;
		const double LN2 = 0.6931471805599453094172321214581766;
		foreach (var v in _doubleData)
			sum += Math.Log2(v) * LN2;
		return sum;
	}

	// ── two-parameter benchmarks (Log(x, base)) ───────────────────────────

	/// <summary>
	/// Built-in Math.Log(x, base) — IEEE-accurate float result (= current optimizer output).
	/// Note: no MathF.Log(float, float) overload exists; uses Math.Log with implicit upcast.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("TwoParam_Float")]
	public float DotNetLog2Param_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += (float)Math.Log(_floatData[i], _baseFloatData[i]);
		return sum;
	}

	/// <summary>
	/// FastLog2Param(float) — optimizer output: FastLog(x) / FastLog(newBase).
	/// log_base(x) = ln(x) / ln(newBase) — two Horner polynomial evaluations + 1 division.
	/// Key FP ops: 2 × (4 FMAs + 1 MUL + 1 bit-cast) + 1 DIV.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("TwoParam_Float")]
	public float FastLog2Param_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += FastLogFloat(_floatData[i]) / FastLogFloat(_baseFloatData[i]);
		return sum;
	}

	/// <summary>
	/// Built-in Math.Log(x, base) — IEEE-accurate double result (= current optimizer output).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("TwoParam_Double")]
	public double DotNetLog2Param_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += Math.Log(_doubleData[i], _baseDoubleData[i]);
		return sum;
	}

	/// <summary>
	/// FastLog2Param(double) — optimizer output: FastLog(x) / FastLog(newBase).
	/// log_base(x) = ln(x) / ln(newBase) — two Horner polynomial evaluations + 1 division.
	/// Key FP ops: 2 × (4 FMAs + 1 MUL + 1 bit-cast) + 1 DIV.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("TwoParam_Double")]
	public double FastLog2Param_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += FastLogDouble(_doubleData[i]) / FastLogDouble(_baseDoubleData[i]);
		return sum;
	}

	// ── private implementations ───────────────────────────────────────────

	/// <summary>
	/// FastLog(float) — Horner scheme.
	/// Bit-extracts base-2 exponent e and mantissa m ∈ [1, 2), then evaluates
	/// a degree-4 minimax polynomial for ln(m).
	/// ln(x) = e·ln(2) + ln(m)
	/// Max relative error ≈ 8.7e-5.
	/// </summary>
	private static float FastLogFloat(float x)
	{
		if (Single.IsNaN(x) || x < 0f) return Single.NaN;
		if (x == 0f) return Single.NegativeInfinity;
		if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

		var bits = BitConverter.SingleToInt32Bits(x);
		var e    = (bits >> 23) - 127;
		var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

		// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const float c4 = -0.056570851f;
		const float c3 =  0.447178975f;
		const float c2 = -1.469956800f;
		const float c1 =  2.821202636f;
		const float c0 = -1.741793927f;

		var lnm = Single.FusedMultiplyAdd(c4, m, c3);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c0);

		const float LN2 = 0.6931471805599453f;  // ln(2)
		return e * LN2 + lnm;
	}

	/// <summary>
	/// FastLogV2(float) — Estrin's scheme.
	/// p(m) = (c0 + c1·m) + m²·(c2 + c3·m) + m⁴·c4.
	/// Independent (lo, hi, m²) pairs can be computed in parallel.
	/// </summary>
	private static float FastLogV2Float(float x)
	{
		if (Single.IsNaN(x) || x < 0f) return Single.NaN;
		if (x == 0f) return Single.NegativeInfinity;
		if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

		var bits = BitConverter.SingleToInt32Bits(x);
		var e    = (bits >> 23) - 127;
		var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

		const float c4 = -0.056570851f;
		const float c3 =  0.447178975f;
		const float c2 = -1.469956800f;
		const float c1 =  2.821202636f;
		const float c0 = -1.741793927f;

		var m2  = m * m;
		var m4  = m2 * m2;
		var lo  = Single.FusedMultiplyAdd(c1, m,  c0);   // c0 + c1·m
		var hi  = Single.FusedMultiplyAdd(c3, m,  c2);   // c2 + c3·m
		var lnm = Single.FusedMultiplyAdd(m2, hi, lo);   // lo + m²·(c2 + c3·m)
		lnm     = Single.FusedMultiplyAdd(m4, c4, lnm);  // + m⁴·c4

		const float LN2 = 0.6931471805599453f;
		return e * LN2 + lnm;
	}

	/// <summary>
	/// FastLog(double) — Horner scheme.
	/// Bit-extracts base-2 exponent e and mantissa m ∈ [1, 2), then evaluates
	/// a degree-4 minimax polynomial for ln(m).
	/// ln(x) = e·ln(2) + ln(m)
	/// Max relative error ≈ 8.7e-5.
	/// </summary>
	private static double FastLogDouble(double x)
	{
		if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
		if (x == 0.0) return Double.NegativeInfinity;
		if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

		var bits = BitConverter.DoubleToInt64Bits(x);
		var e    = (int)((bits >> 52) - 1023L);
		var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

		// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const double c4 = -0.056570851;
		const double c3 =  0.447178975;
		const double c2 = -1.469956800;
		const double c1 =  2.821202636;
		const double c0 = -1.741793927;

		var lnm = Double.FusedMultiplyAdd(c4, m, c3);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c0);

		const double LN2 = 0.6931471805599453094172321214581766;  // ln(2)
		return e * LN2 + lnm;
	}

	/// <summary>
	/// FastLogV2(double) — Estrin's scheme.
	/// p(m) = (c0 + c1·m) + m²·(c2 + c3·m) + m⁴·c4.
	/// Independent (lo, hi, m²) pairs can be computed in parallel.
	/// </summary>
	private static double FastLogV2Double(double x)
	{
		if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
		if (x == 0.0) return Double.NegativeInfinity;
		if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

		var bits = BitConverter.DoubleToInt64Bits(x);
		var e    = (int)((bits >> 52) - 1023L);
		var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

		const double c4 = -0.056570851;
		const double c3 =  0.447178975;
		const double c2 = -1.469956800;
		const double c1 =  2.821202636;
		const double c0 = -1.741793927;

		var m2  = m * m;
		var m4  = m2 * m2;
		var lo  = Double.FusedMultiplyAdd(c1, m,  c0);   // c0 + c1·m
		var hi  = Double.FusedMultiplyAdd(c3, m,  c2);   // c2 + c3·m
		var lnm = Double.FusedMultiplyAdd(m2, hi, lo);   // lo + m²·(c2 + c3·m)
		lnm     = Double.FusedMultiplyAdd(m4, c4, lnm);  // + m⁴·c4

		const double LN2 = 0.6931471805599453094172321214581766;
		return e * LN2 + lnm;
	}
}


