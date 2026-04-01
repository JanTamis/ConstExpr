using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar TanPi implementations for float and double.
///
/// TanPi(x) = Tan(π·x), period = 1, asymptotes at x = ±0.5 + k.
///
/// Three type groups are benchmarked independently:
///   Float  – float.TanPi          (baseline: hardware-accurate)
///            CurrentFastTanPi     (2 NaN/Inf guards + Round + fallback at |x|>0.45 → TanPi + [2/2] Padé at πx)
///            FastTanPiV2          (no guards, Round + Min-fold to [0,0.25] + [2/2] Padé at π·xf, reciprocal)
///            FastTanPiV3          (no guards, Round + Min-fold + absorbed-π [2/2] Padé at xf, 1 fewer FMUL)
///   Double – double.TanPi         (baseline: hardware-accurate)
///            CurrentFastTanPi     (2 NaN/Inf guards + Round + fallback at |x|>0.45 → TanPi + [3/3] Padé at πx)
///            FastTanPiV2          (no guards, Round + Min-fold to [0,0.25] + [2/3] Padé at π·xf, reciprocal)
///            FastTanPiV3          (no guards, Round + Min-fold + absorbed-π [2/3] Padé at xf, 1 fewer FMUL)
///
/// Current implementation issues:
///   1. Two NaN/Infinity guards add branch overhead on every call (always false for finite inputs).
///   2. Asymptote fallback at |xReduced|>0.45 calls full TanPi (~2.5 ns) for ≈10% of inputs.
///        fraction = 2·(0.5 − 0.45) / 1.0 = 10%.
///   3. Convert-to-radians step (xReduced·π) is on the hot path.
///
/// V2 key design — fold + Padé rational, no guards, no fallback:
///   Range-reduce to [−0.5, 0.5] via Round(x).
///   Fold |x| > 0.25 via cotangent identity: tanPi(x) = 1/tanPi(0.5−x).
///   Float [2/2] Padé: tan(v)/v ≈ (1 − v²/9 + v⁴/945) / (1 − 4v²/9 + v⁴/63)  at v=π·xf ∈ [0,π/4]
///   Double [2/3] Padé: same coefficients as TanBenchmark V2.
///   The cotangent fold eliminates the 10% fallback path at the cost of a 50%-taken swap branch.
///
/// V3 key design — absorbed-π polynomial:
///   Same fold as V2 but substitute v = π·xf and absorb π into numerator coefficients.
///   tanPi(xf) = xf · (C₁ + C₃·xf² + C₅·xf⁴) / (1 + D₂·xf² + D₄·xf⁴)
///   Coefficients:  C₁ = π,  C₃ = −π³/9,  C₅ = π⁵/945  (float [2/2])
///                  D₂ = −4π²/9,  D₄ = π⁴/63
///   Saves the explicit xf·π FMUL present in V2 → one fewer operation on the hot path.
///   Double uses analogous [2/3] coefficients.
///
/// Input domain: [−100, 100] — spans 200 full TanPi periods, exercises range reduction heavily.
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64):
///   Double: DotNet=3.410 ns | Current=1.513 ns (−56%) | V2=1.248 ns (−63%) | V3=1.227 ns (−64%) ← winner
///   Float:  DotNet=2.477 ns | Current=1.266 ns (−49%) | V2=1.179 ns (−52%) | V3≈1.155 ns (−53%) ← winner
///
/// Conclusion: V3 (absorbed-π cotangent-fold) is the fastest for both types.
///   1. Removes the 2 NaN/Inf guards (always false for finite inputs).
///   2. Eliminates the 10% asymptote fallback — cotangent fold covers x ∈ (0.25, 0.5) without TanPi().
///   3. Saves the explicit xf·π FMUL — π is absorbed into the Padé numerator coefficients.
///   V3 replaces the previous implementation in TanPiFunctionOptimizer.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*TanPiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TanPiBenchmark
{
	// 1 024 values spread uniformly over many TanPi periods to exercise range reduction.
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
			var v          = rng.NextDouble() * 200.0 - 100.0; // uniform in [−100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.TanPi — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetTanPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.TanPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastTanPi(float) — exact mirror of TanPiFunctionOptimizer output.
	/// Two NaN/Inf guards + Round range reduction + asymptote fallback at |xReduced|>0.45 (~10% of inputs).
	/// Main path: convert to radians (1 FMUL) + rational [2/2] P(v²)/Q(v²) at v = πx (4 FMA + 1 div).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastTanPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastTanPiFloat(v);
		return sum;
	}

	/// <summary>
	/// FastTanPiV2(float) — fold to [0,0.25] + [2/2] Padé at π·xf, reciprocal for folded half.
	/// Padé coefficients: p₁=−1/9, p₂=1/945 | q₁=−4/9, q₂=1/63 (matched to tan Taylor through x⁸).
	/// No NaN/Inf guards; no asymptote fallback; 4 FMA + 1 FMUL + 1 div on main path.
	/// Swap branch (50%-taken) eliminates the 10% fallback at the cost of mispredictions.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanPiV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastTanPiV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastTanPiV3(float) — same fold as V2 but absorbed-π [2/2] Padé evaluated at xf ∈ [0,0.25].
	/// tanPi(xf) = xf·(π − π³/9·xf² + π⁵/945·xf⁴) / (1 − 4π²/9·xf² + π⁴/63·xf⁴)
	/// Saves the explicit xf·π FMUL vs V2 → 4 FMA + 1 div on the hot path (no radians conversion).
	/// Max absolute error ≈ 4e−5 on [0, π/4] — acceptable for FastMath use.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanPiV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastTanPiV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in double.TanPi — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetTanPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.TanPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastTanPi(double) — exact mirror of TanPiFunctionOptimizer output.
	/// Two NaN/Inf guards + Round range reduction + asymptote fallback at |xReduced|>0.45 (~10% of inputs).
	/// Main path: convert to radians + rational [3/3] P(v²)/Q(v²) at v = πx (6 FMA + 1 div).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastTanPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastTanPiDouble(v);
		return sum;
	}

	/// <summary>
	/// FastTanPiV2(double) — fold to [0,0.25] + [2/3] Padé at π·xf, reciprocal for folded half.
	/// Padé coefficients: a₁=−4/33, a₂=1/495 | b₁=−5/11, b₂=2/99, b₃=−1/10395.
	/// No NaN/Inf guards; no asymptote fallback; 6 FMA + 1 FMUL + 1 div on main path.
	/// Max absolute error ≈ 1e−5 on [0, π/4].
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanPiV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastTanPiV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastTanPiV3(double) — same fold as V2 but absorbed-π [2/3] Padé evaluated at xf ∈ [0,0.25].
	/// tanPi(xf) = xf·(π − 4π³/33·xf² + π⁵/495·xf⁴) / (1 − 5π²/11·xf² + 2π⁴/99·xf⁴ − π⁶/10395·xf⁶)
	/// Saves the explicit xf·π FMUL vs V2 → 6 FMA + 1 div (no radians conversion).
	/// Max absolute error ≈ 1e−5 on [0, π/4].
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanPiV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastTanPiV3Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	// ---- current (mirror of TanPiFunctionOptimizer output) ----------------

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastTanPiFloat(float x)
	{
		// NaN/Inf guards (always false for finite inputs from our dataset)
		if (Single.IsNaN(x)) return Single.NaN;
		if (Single.IsInfinity(x)) return Single.NaN;

		// Range reduce to [−0.5, 0.5] — TanPi has period 1
		var xReduced = x - Single.Round(x);

		// Asymptote fallback: ~10% of inputs hit this slow path
		var absX = Single.Abs(xReduced);
		if (absX > 0.45f)
			return Single.TanPi(x);

		// Convert to radians: v = xReduced·π ∈ (−0.45π, 0.45π)
		var xRadians = xReduced * Single.Pi;
		var x2       = xRadians * xRadians;

		// [2/2] Padé: tan(v)/v ≈ (1 + p1·v² + p2·v⁴) / (1 + q1·v² + q2·v⁴)
		var p1        = -0.1306282f;
		var p2        =  0.0052854f;
		var numerator = Single.FusedMultiplyAdd(p2, x2, p1);
		numerator     = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
		numerator    *= xRadians;

		var q1          = -0.4636476f;
		var q2          =  0.0157903f;
		var denominator = Single.FusedMultiplyAdd(q2, x2, q1);
		denominator     = Single.FusedMultiplyAdd(denominator, x2, 1.0f);

		return numerator / denominator;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastTanPiDouble(double x)
	{
		if (Double.IsNaN(x)) return Double.NaN;
		if (Double.IsInfinity(x)) return Double.NaN;

		var xReduced = x - Double.Round(x);

		var absX = Double.Abs(xReduced);
		if (absX > 0.45)
			return Double.TanPi(x);

		var xRadians = xReduced * Double.Pi;
		var x2       = xRadians * xRadians;

		var p1        = -0.13089944486966634;
		var p2        =  0.005405742881796775;
		var p3        = -0.00010606776596208569;
		var numerator = Double.FusedMultiplyAdd(p3, x2, p2);
		numerator     = Double.FusedMultiplyAdd(numerator, x2, p1);
		numerator     = Double.FusedMultiplyAdd(numerator, x2, 1.0);
		numerator    *= xRadians;

		var q1          = -0.46468849716162905;
		var q2          =  0.015893657956882884;
		var q3          = -0.00031920703894961204;
		var denominator = Double.FusedMultiplyAdd(q3, x2, q2);
		denominator     = Double.FusedMultiplyAdd(denominator, x2, q1);
		denominator     = Double.FusedMultiplyAdd(denominator, x2, 1.0);

		return numerator / denominator;
	}

	// ---- V2: fold to [0,0.25] + Padé at π·xf + reciprocal ---------------

	/// <summary>
	/// Cotangent fold: tanPi(x) = 1/tanPi(0.5−x) for x ∈ (0.25, 0.5).
	/// Folds the dangerous [0.25, 0.5) region to safe [0, 0.25] where the
	/// [2/2] Padé is accurate — eliminating the asymptote fallback entirely.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastTanPiV2Float(float x)
	{
		// Range reduce to [−0.5, 0.5] — TanPi period is 1
		x -= Single.Round(x);

		var signX = x;
		x         = Single.Abs(x); // [0, 0.5]

		// Fold (0.25, 0.5) → [0, 0.25] via cotangent: tanPi(x) = 1/tanPi(0.5−x)
		var swap = x > 0.25f;
		var xf   = swap ? 0.5f - x : x; // [0, 0.25]

		// Convert to radians: v = xf·π ∈ [0, π/4]
		var xr = xf * Single.Pi;
		var x2 = xr * xr;

		// [2/2] Padé: tan(v)/v ≈ (1 − v²/9 + v⁴/945) / (1 − 4v²/9 + v⁴/63)
		var num = Single.FusedMultiplyAdd(1f / 945f, x2, -1f / 9f);
		num     = Single.FusedMultiplyAdd(num, x2, 1.0f);
		num    *= xr;
		var den = Single.FusedMultiplyAdd(1f / 63f, x2, -4f / 9f);
		den     = Single.FusedMultiplyAdd(den, x2, 1.0f);

		var t = num / den;

		// Reciprocal for folded inputs: tanPi(x) = 1/tanPi(0.5−x)
		if (swap) t = 1.0f / t;

		return Single.CopySign(t, signX);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastTanPiV2Double(double x)
	{
		x -= Double.Round(x);

		var signX = x;
		x         = Double.Abs(x);

		var swap = x > 0.25;
		var xf   = swap ? 0.5 - x : x;

		var xr = xf * Double.Pi;
		var x2 = xr * xr;

		// [2/3] Padé: tan(v)/v ≈ (1 − 4v²/33 + v⁴/495) / (1 − 5v²/11 + 2v⁴/99 − v⁶/10395)
		var num = Double.FusedMultiplyAdd(1.0 / 495.0, x2, -4.0 / 33.0);
		num     = Double.FusedMultiplyAdd(num, x2, 1.0);
		num    *= xr;

		var den = Double.FusedMultiplyAdd(-1.0 / 10395.0, x2, 2.0 / 99.0);
		den     = Double.FusedMultiplyAdd(den, x2, -5.0 / 11.0);
		den     = Double.FusedMultiplyAdd(den, x2, 1.0);

		var t = num / den;
		if (swap) t = 1.0 / t;

		return Double.CopySign(t, signX);
	}

	// ---- V3: fold + absorbed-π Padé, saves 1 FMUL (no xf·π conversion) --

	// Float [2/2] absorbed-π coefficients (v = π·xf substituted, π factored into numerator):
	//   num = xf · (C₁ + C₃·xf² + C₅·xf⁴)  where Cₙ = (πⁿ/n!-derived Padé coeff)·π
	//   den = 1 + D₂·xf² + D₄·xf⁴
	//   C₁ = π,  C₃ = −π³/9,  C₅ = π⁵/945
	//   D₂ = −4π²/9,  D₄ = π⁴/63

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastTanPiV3Float(float x)
	{
		x -= Single.Round(x);

		var signX = x;
		x         = Single.Abs(x);

		var swap = x > 0.25f;
		var xf   = swap ? 0.5f - x : x; // [0, 0.25]
		var u2   = xf * xf;

		// Absorbed-π [2/2] Padé evaluated at xf (not at π·xf)
		const float C1 =  3.14159265f;   //  π
		const float C3 = -3.44514185f;   // −π³/9
		const float C5 =  0.32383247f;   //  π⁵/945
		const float D2 = -4.38649084f;   // −4π²/9
		const float D4 =  1.54617606f;   //  π⁴/63

		var num = Single.FusedMultiplyAdd(C5, u2, C3);
		num     = Single.FusedMultiplyAdd(num, u2, C1);
		num    *= xf;
		var den = Single.FusedMultiplyAdd(D4, u2, D2);
		den     = Single.FusedMultiplyAdd(den, u2, 1.0f);

		var t = num / den;
		if (swap) t = 1.0f / t;

		return Single.CopySign(t, signX);
	}

	// Double [2/3] absorbed-π coefficients:
	//   C₁ = π,  C₃ = −4π³/33,  C₅ = π⁵/495
	//   D₂ = −5π²/11,  D₄ = 2π⁴/99,  D₆ = −π⁶/10395

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastTanPiV3Double(double x)
	{
		x -= Double.Round(x);

		var signX = x;
		x         = Double.Abs(x);

		var swap = x > 0.25;
		var xf   = swap ? 0.5 - x : x;
		var u2   = xf * xf;

		// Absorbed-π [2/3] Padé evaluated at xf (not at π·xf)
		const double C1 =  3.14159265358979324;   //  π
		const double C3 = -3.75833657307876;       // −4π³/33
		const double C5 =  0.61822157532380;       //  π⁵/495
		const double D2 = -4.48618381867698;       // −5π²/11
		const double D4 =  1.96786042492934;       //  2π⁴/99
		const double D6 = -0.09248641780;          // −π⁶/10395

		var num = Double.FusedMultiplyAdd(C5, u2, C3);
		num     = Double.FusedMultiplyAdd(num, u2, C1);
		num    *= xf;

		var den = Double.FusedMultiplyAdd(D6, u2, D4);
		den     = Double.FusedMultiplyAdd(den, u2, D2);
		den     = Double.FusedMultiplyAdd(den, u2, 1.0);

		var t = num / den;
		if (swap) t = 1.0 / t;

		return Double.CopySign(t, signX);
	}
}


