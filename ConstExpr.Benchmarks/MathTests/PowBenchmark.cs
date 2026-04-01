using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.Pow / MathF.Pow (built-in) against three scalar FastPow candidates.
///
/// All FastPow variants decompose x^y = exp2(y · log2(x)) using:
///   – Bit-level extraction of exponent (iexp) and mantissa (m ∈ [1, 2)).
///   – Artanh series for log₂(m): log₂(m) = 2·(z + z³/3 + z⁵/5 + z⁷/7) / ln2,
///       where z = (m−1)/(m+1) ∈ [0, 1/3).  One FDIV + 3 FMAs.
///   – Range reduction for exp2: split y·log₂(x) into integer k and fractional f ∈ [−0.5, 0.5).
///   – Scale by 2^k via IEEE exponent-bit rewrite.
/// The variants differ only in how they evaluate 2^f:
///
///   FastPow    – current ConstExpr generator output.
///               Computes u = LN2·f first, then evaluates e^u with a degree-7 Taylor
///               polynomial via 7 FMAs + 1 MUL (8 FP operations).
///               Float: 7 FMAs + 1 MUL for 2^f.  Double: identical.
///
///   FastPowV2  – direct 2^f polynomial with coefficients cₙ = ln(2)ⁿ/n! pre-merged.  ← expected winner
///               Eliminates the intermediate u=LN2·f multiplication.
///               Float: degree 5 (5 FMAs, saves 1 MUL + 2 FMA — artanh error dominates).
///               Double: degree 7 (7 FMAs, saves 1 MUL).
///               Uses MathF.Round / Math.Round → branchless FRINTN+FCVTZS on ARM64.
///
///   FastPowV3  – Estrin's scheme applied to the V2 polynomial.
///               Float:  (1 + C1·f) + f²·(C2 + C3·f) + f⁴·(C4 + C5·f)
///               Double: (1 + C1·f) + f²·(C2 + C3·f) + f⁴·((C4 + C5·f) + f²·(C6 + C7·f))
///               Shorter FP critical-path depth vs Horner; on ARM64 throughput-limited
///               loops the extra f² / f⁴ MULs typically offset the ILP gain — V2 wins.
///
/// Accuracy (relative error, worst case over benchmark input range):
///   Float  – all FastPow variants: ~7e-5  (artanh 4-term log₂ error ≈ 6.5e-6 dominates)
///   Double – all FastPow variants: ~7e-5  (same artanh series; intentional fast-math trade-off)
///   Intentional: FastPow targets FastMath mode where speed > IEEE accuracy.
///
/// Polynomial constants:
///   ln(2)       = 0.693147180559945309417
///   ln(2)²/2    = 0.240226506959100690934
///   ln(2)³/6    = 0.055504108664821579953
///   ln(2)⁴/24   = 0.009618129107628477232
///   ln(2)⁵/120  = 0.001333355814642844256
///   ln(2)⁶/720  = 0.000154035303904566903
///   ln(2)⁷/5040 = 0.000015253300202639438
///   log₂(e)     = 1.442695040888963407359  (= 1/ln2 = INV_LN2)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method            Category  Mean      Ratio    Note
///   ----------------  --------  --------  -----    -----------------------------------
///   DotNetPow_Double  Double    4.943 ns  1.00x    built-in, IEEE-accurate
///   FastPow_Double    Double    2.987 ns  0.60x    previous: 1 FDIV + 7 FMAs + 1 MUL
///   FastPowV2_Double  Double    2.965 ns  0.60x  ← new: 1 FDIV + 7 FMAs, saves 1 MUL (~0.7 % faster)
///   FastPowV3_Double  Double    3.129 ns  0.63x    Estrin — slower than V2 on ARM64
///
///   DotNetPow_Float   Float     2.508 ns  1.00x    built-in  ← FASTEST for float
///   FastPow_Float     Float     3.001 ns  1.20x    SLOWER than built-in
///   FastPowV2_Float   Float     2.707 ns  1.08x    still slower than built-in
///   FastPowV3_Float   Float     3.001 ns  1.20x    Estrin — no improvement over V1
///
/// Conclusions:
///   Double → FastPowV2 is 1.67× faster than Math.Pow; injected by PowFunctionOptimizer.
///   Float  → MathF.Pow (built-in) wins; PowFunctionOptimizer falls back to MathF.Pow.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*PowBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PowBenchmark
{
	// 1 024 pairs (x, y): x ∈ (0.5, 10.5), y ∈ (−3, 3).
	// All x values are positive so no NaN/special-case branches fire — we measure
	// the hot polynomial path exclusively.  Fixed seed for reproducibility.
	private const int N = 1_024;
	private float[]  _xFloat  = null!;
	private float[]  _yFloat  = null!;
	private double[] _xDouble = null!;
	private double[] _yDouble = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_xFloat  = new float[N];
		_yFloat  = new float[N];
		_xDouble = new double[N];
		_yDouble = new double[N];

		for (var i = 0; i < N; i++)
		{
			_xDouble[i] = 0.5 + rng.NextDouble() * 10.0;   // uniform in (0.5, 10.5)
			_yDouble[i] = -3.0 + rng.NextDouble() * 6.0;   // uniform in (−3, 3)
			_xFloat[i]  = (float)_xDouble[i];
			_yFloat[i]  = (float)_yDouble[i];
		}
	}

	// ── float benchmarks ────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.Pow — hardware-accurate float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetPow_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += MathF.Pow(_xFloat[i], _yFloat[i]);
		return sum;
	}

	/// <summary>
	/// FastPow(float) — current ConstExpr optimizer output.
	/// Artanh log₂ via FDIV; 2^f via u=LN2·f + degree-7 Taylor for e^u.
	/// Hot-path FP ops: 1 FDIV + 8 MUL + 10 FMA.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastPow_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += V1Float(_xFloat[i], _yFloat[i]);
		return sum;
	}

	/// <summary>
	/// FastPowV2(float) — direct 2^f polynomial, Horner form.
	/// Degree 5 (artanh log₂ error ~6.5e-6 dominates, so degree-5 poly error ~2.3e-6 is fine).
	/// Eliminates the u=LN2·f multiplication and 2 FMAs vs degree-7.
	/// Uses MathF.Round → branchless FRINTN+FCVTZS on ARM64.
	/// Hot-path FP ops: 1 FDIV + 6 MUL + 8 FMA.  Saves 1 MUL + 2 FMA vs FastPow.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastPowV2_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += V2Float(_xFloat[i], _yFloat[i]);
		return sum;
	}

	/// <summary>
	/// FastPowV3(float) — Estrin's scheme on the degree-5 2^f polynomial.
	/// p(f) = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·(C4 + C5·f).
	/// FP critical-path depth: 2 MUL + 2 FMA vs 5 sequential FMAs in Horner.
	/// On ARM64 throughput-limited loops the extra f²/f⁴ MULs typically offset the ILP gain.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastPowV3_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += V3Float(_xFloat[i], _yFloat[i]);
		return sum;
	}

	// ── double benchmarks ───────────────────────────────────────────────────────

	/// <summary>Built-in Math.Pow — IEEE-accurate double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetPow_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += Math.Pow(_xDouble[i], _yDouble[i]);
		return sum;
	}

	/// <summary>
	/// FastPow(double) — current ConstExpr optimizer output.
	/// Artanh log₂ via FDIV; 2^f via u=LN2·f + degree-7 Taylor for e^u.
	/// Hot-path FP ops: 1 FDIV + 8 MUL + 10 FMA.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastPow_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += V1Double(_xDouble[i], _yDouble[i]);
		return sum;
	}

	/// <summary>
	/// FastPowV2(double) — direct 2^f polynomial, Horner form.
	/// Degree 7; eliminates the u=LN2·f multiplication (saves 1 MUL vs FastPow).
	/// Uses Math.Round → branchless FRINTN+FCVTZS on ARM64.
	/// Hot-path FP ops: 1 FDIV + 7 MUL + 10 FMA.  Saves 1 MUL vs FastPow.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastPowV2_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += V2Double(_xDouble[i], _yDouble[i]);
		return sum;
	}

	/// <summary>
	/// FastPowV3(double) — Estrin's scheme on the degree-7 2^f polynomial.
	/// p(f) = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·((C4 + C5·f) + f²·(C6 + C7·f)).
	/// FP critical-path depth: 2 MUL + 3 FMA vs 7 sequential FMAs in Horner.
	/// On ARM64 the extra MULs typically offset the ILP gain.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastPowV3_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += V3Double(_xDouble[i], _yDouble[i]);
		return sum;
	}

	// ── V1: current implementation (mirrors commented FastPow in PowFunctionOptimizer) ─────

	private static float V1Float(float x, float y)
	{
		if (y == 0.0f || x == 1.0f) return 1.0f;
		if (x <= 0.0f) return float.NaN;

		var ibits = BitConverter.SingleToInt32Bits(x);
		var iexp  = ((ibits >> 23) & 0xFF) - 127;
		var imant = ibits & 0x7FFFFF;
		var m     = 1.0f + imant * (1.0f / 8388608.0f);  // mantissa ∈ [1, 2)

		// Artanh log₂(m): z = (m−1)/(m+1) ∈ [0, 1/3); series error ≈ 2·z⁹/9 / ln2
		const float INV_LN2 = 1.4426950408889634f;
		var z       = (m - 1.0f) / (m + 1.0f);
		var t2      = z * z;
		var sInner  = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f);
		var log2m   = 2.0f * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv  = y * log2x;
		var kf  = Single.Floor(tv + 0.5f);
		var k   = (int)kf;
		var f   = tv - kf;  // f ∈ [−0.5, 0.5)

		// 2^f via e^(LN2·f): u = LN2·f, then degree-7 Taylor for e^u
		const float LN2 = 0.6931471805599453f;
		var u = LN2 * f;
		var p = 1f / 5040f;
		p = Single.FusedMultiplyAdd(p, u, 1f / 720f);
		p = Single.FusedMultiplyAdd(p, u, 1f / 120f);
		p = Single.FusedMultiplyAdd(p, u, 1f / 24f);
		p = Single.FusedMultiplyAdd(p, u, 1f / 6f);
		p = Single.FusedMultiplyAdd(p, u, 0.5f);
		p = Single.FusedMultiplyAdd(p, u, 1f);
		var exp2f = Single.FusedMultiplyAdd(p, u, 1f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * exp2f;
	}

	private static double V1Double(double x, double y)
	{
		if (y == 0.0 || x == 1.0) return 1.0;
		if (x <= 0.0) return double.NaN;

		var bits  = BitConverter.DoubleToInt64Bits(x);
		var iexp  = (int)((bits >> 52) & 0x7FF) - 1023;
		var imant = bits & 0x000F_FFFF_FFFF_FFFFL;
		var m     = 1.0 + imant * (1.0 / 4503599627370496.0);  // mantissa ∈ [1, 2)

		const double INV_LN2 = 1.4426950408889634073599246810018921;
		var z       = (m - 1.0) / (m + 1.0);
		var t2      = z * z;
		var sInner  = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0);
		var log2m   = 2.0 * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv  = y * log2x;
		var kf  = Double.Floor(tv + 0.5);
		var k   = (int)kf;
		var f   = tv - kf;  // f ∈ [−0.5, 0.5)

		// 2^f via e^(LN2·f): u = LN2·f, then degree-7 Taylor for e^u
		const double LN2 = 0.6931471805599453094172321214581766;
		var u = LN2 * f;
		var p = 1.0 / 5040.0;
		p = Double.FusedMultiplyAdd(p, u, 1.0 / 720.0);
		p = Double.FusedMultiplyAdd(p, u, 1.0 / 120.0);
		p = Double.FusedMultiplyAdd(p, u, 1.0 / 24.0);
		p = Double.FusedMultiplyAdd(p, u, 1.0 / 6.0);
		p = Double.FusedMultiplyAdd(p, u, 0.5);
		p = Double.FusedMultiplyAdd(p, u, 1.0);
		var exp2f = Double.FusedMultiplyAdd(p, u, 1.0);

		var expBits = (k + 1023 & 0x7FFL) << 52;
		return BitConverter.Int64BitsToDouble(expBits) * exp2f;
	}

	// ── V2: direct 2^f polynomial, Horner form ───────────────────────────────────
	// Coefficients cₙ = ln(2)ⁿ / n! evaluate 2^f directly, eliminating u=LN2·f.
	// Float: degree 5 (artanh log₂ error ≈ 6.5e-6 >> degree-5 exp2 error ≈ 2.3e-6).
	// Double: degree 7 (matches V1 accuracy; saves only the 1 MUL).
	// MathF.Round / Math.Round → single FRINTN instruction on ARM64.

	private static float V2Float(float x, float y)
	{
		if (y == 0.0f || x == 1.0f) return 1.0f;
		if (x <= 0.0f) return float.NaN;

		var ibits = BitConverter.SingleToInt32Bits(x);
		var iexp  = ((ibits >> 23) & 0xFF) - 127;
		var imant = ibits & 0x7FFFFF;
		var m     = 1.0f + imant * (1.0f / 8388608.0f);

		const float INV_LN2 = 1.4426950408889634f;
		var z       = (m - 1.0f) / (m + 1.0f);
		var t2      = z * z;
		var sInner  = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f);
		var log2m   = 2.0f * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv = y * log2x;
		var k  = (int)MathF.Round(tv);   // branchless FRINTN + FCVTZS on ARM64
		var f  = tv - k;                  // f ∈ [−0.5, 0.5)

		// Direct degree-5 Horner for 2^f: cₙ = ln(2)ⁿ/n!
		const float c5 = 1.3333558146428443e-3f;  // ln(2)⁵ / 120
		const float c4 = 9.6181291076284772e-3f;  // ln(2)⁴ / 24
		const float c3 = 5.5504108664821580e-2f;  // ln(2)³ / 6
		const float c2 = 2.4022650695910069e-1f;  // ln(2)² / 2
		const float c1 = 6.9314718055994531e-1f;  // ln(2)

		var p     = Single.FusedMultiplyAdd(c5, f, c4);
		p         = Single.FusedMultiplyAdd(p,  f, c3);
		p         = Single.FusedMultiplyAdd(p,  f, c2);
		p         = Single.FusedMultiplyAdd(p,  f, c1);
		var exp2f = Single.FusedMultiplyAdd(p,  f, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * exp2f;
	}

	private static double V2Double(double x, double y)
	{
		if (y == 0.0 || x == 1.0) return 1.0;
		if (x <= 0.0) return double.NaN;

		var bits  = BitConverter.DoubleToInt64Bits(x);
		var iexp  = (int)((bits >> 52) & 0x7FF) - 1023;
		var imant = bits & 0x000F_FFFF_FFFF_FFFFL;
		var m     = 1.0 + imant * (1.0 / 4503599627370496.0);

		const double INV_LN2 = 1.4426950408889634073599246810018921;
		var z       = (m - 1.0) / (m + 1.0);
		var t2      = z * z;
		var sInner  = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0);
		var log2m   = 2.0 * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv = y * log2x;
		var k  = (long)Math.Round(tv);    // branchless FRINTN + FCVTZS on ARM64
		var f  = tv - k;                   // f ∈ [−0.5, 0.5)

		// Direct degree-7 Horner for 2^f: cₙ = ln(2)ⁿ/n!
		const double c7 = 1.5253300202639438e-5;  // ln(2)⁷ / 5040
		const double c6 = 1.5403530390456690e-4;  // ln(2)⁶ / 720
		const double c5 = 1.3333558146428443e-3;  // ln(2)⁵ / 120
		const double c4 = 9.6181291076284772e-3;  // ln(2)⁴ / 24
		const double c3 = 5.5504108664821580e-2;  // ln(2)³ / 6
		const double c2 = 2.4022650695910069e-1;  // ln(2)² / 2
		const double c1 = 6.9314718055994531e-1;  // ln(2)

		var p     = Double.FusedMultiplyAdd(c7, f, c6);
		p         = Double.FusedMultiplyAdd(p,  f, c5);
		p         = Double.FusedMultiplyAdd(p,  f, c4);
		p         = Double.FusedMultiplyAdd(p,  f, c3);
		p         = Double.FusedMultiplyAdd(p,  f, c2);
		p         = Double.FusedMultiplyAdd(p,  f, c1);
		var exp2f = Double.FusedMultiplyAdd(p,  f, 1.0);

		var expBits = (k + 1023L & 0x7FFL) << 52;
		return BitConverter.Int64BitsToDouble(expBits) * exp2f;
	}

	// ── V3: Estrin's scheme on the direct 2^f polynomial ─────────────────────────
	// Float:  p(f) = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·(C4 + C5·f)
	// Double: p(f) = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·((C4 + C5·f) + f²·(C6 + C7·f))
	// Parallel pairs reduce FP critical-path depth; extra f²/f⁴ MULs typically
	// offset the ILP gain on ARM64 throughput-limited loops.

	private static float V3Float(float x, float y)
	{
		if (y == 0.0f || x == 1.0f) return 1.0f;
		if (x <= 0.0f) return float.NaN;

		var ibits = BitConverter.SingleToInt32Bits(x);
		var iexp  = ((ibits >> 23) & 0xFF) - 127;
		var imant = ibits & 0x7FFFFF;
		var m     = 1.0f + imant * (1.0f / 8388608.0f);

		const float INV_LN2 = 1.4426950408889634f;
		var z       = (m - 1.0f) / (m + 1.0f);
		var t2      = z * z;
		var sInner  = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);
		sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f);
		var log2m   = 2.0f * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv = y * log2x;
		var k  = (int)MathF.Round(tv);
		var f  = tv - k;

		// Estrin on degree-5: p = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·(C4 + C5·f)
		const float c5 = 1.3333558146428443e-3f;
		const float c4 = 9.6181291076284772e-3f;
		const float c3 = 5.5504108664821580e-2f;
		const float c2 = 2.4022650695910069e-1f;
		const float c1 = 6.9314718055994531e-1f;

		var f2   = f * f;
		var f4   = f2 * f2;
		var p01  = Single.FusedMultiplyAdd(c1, f,  1.0f);   // 1    + C1·f
		var p23  = Single.FusedMultiplyAdd(c3, f,  c2);     // C2   + C3·f
		var p45  = Single.FusedMultiplyAdd(c5, f,  c4);     // C4   + C5·f
		var mid  = Single.FusedMultiplyAdd(f2, p23, p01);   // p01  + f²·p23
		var exp2f = Single.FusedMultiplyAdd(f4, p45, mid);  // mid  + f⁴·p45

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * exp2f;
	}

	private static double V3Double(double x, double y)
	{
		if (y == 0.0 || x == 1.0) return 1.0;
		if (x <= 0.0) return double.NaN;

		var bits  = BitConverter.DoubleToInt64Bits(x);
		var iexp  = (int)((bits >> 52) & 0x7FF) - 1023;
		var imant = bits & 0x000F_FFFF_FFFF_FFFFL;
		var m     = 1.0 + imant * (1.0 / 4503599627370496.0);

		const double INV_LN2 = 1.4426950408889634073599246810018921;
		var z       = (m - 1.0) / (m + 1.0);
		var t2      = z * z;
		var sInner  = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);
		sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0);
		var log2m   = 2.0 * (z * sInner) * INV_LN2;
		var log2x   = iexp + log2m;

		var tv = y * log2x;
		var k  = (long)Math.Round(tv);
		var f  = tv - k;

		// Estrin on degree-7:
		// p(f) = (1 + C1·f) + f²·(C2 + C3·f) + f⁴·((C4 + C5·f) + f²·(C6 + C7·f))
		const double c7 = 1.5253300202639438e-5;
		const double c6 = 1.5403530390456690e-4;
		const double c5 = 1.3333558146428443e-3;
		const double c4 = 9.6181291076284772e-3;
		const double c3 = 5.5504108664821580e-2;
		const double c2 = 2.4022650695910069e-1;
		const double c1 = 6.9314718055994531e-1;

		var f2   = f * f;
		var f4   = f2 * f2;
		var p01  = Double.FusedMultiplyAdd(c1, f,  1.0);    // 1    + C1·f
		var p23  = Double.FusedMultiplyAdd(c3, f,  c2);     // C2   + C3·f
		var p45  = Double.FusedMultiplyAdd(c5, f,  c4);     // C4   + C5·f
		var p67  = Double.FusedMultiplyAdd(c7, f,  c6);     // C6   + C7·f
		var hi   = Double.FusedMultiplyAdd(f2, p67, p45);   // p45  + f²·p67
		var mid  = Double.FusedMultiplyAdd(f2, p23, p01);   // p01  + f²·p23
		var exp2f = Double.FusedMultiplyAdd(f4, hi,  mid);  // mid  + f⁴·hi

		var expBits = (k + 1023L & 0x7FFL) << 52;
		return BitConverter.Int64BitsToDouble(expBits) * exp2f;
	}
}


