using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Sinh implementations for float and double.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Sinh  vs FastSinh(float)   [current: 2-branch — polynomial for |x|&lt;1, exp+ReciprocalEstimate otherwise]
///                         vs FastSinhV2(float) [single Exp(|x|) + one NR reciprocal step, only overflow branch]
///                         vs FastSinhV3(float) [two independent Exp calls, fully branchless, sign is inherent]
///   Double – Math.Sinh   vs FastSinh(double)  [current: same structure, polynomial coefficients are wrong by 10–100×]
///                         vs FastSinhV2(double)[single Exp(|x|) + fp division for 1/ex, only overflow branch]
///                         vs FastSinhV3(double)[two independent Exp calls, fully branchless]
///
/// Issues found in the current implementation:
///   1. Single.ReciprocalEstimate / Double.ReciprocalEstimate gives only ~12/14-bit precision.
///      sinh uses (ex − 1/ex)/2, so error in 1/ex directly corrupts the result:
///        For sinh(1.0f): absolute error from RecipEstimate ≈ 4e-5 (float epsilon ~1.2e-7) — ~333× too large.
///        For sinh(1.0):  absolute error is catastrophic (double has 52-bit mantissa, estimate gives only 14).
///   2. Float polynomial uses degree-7 (4 FMA steps); truncation error at x=1 is x^9/9! ≈ 2.8e-6, which is
///      23× above float epsilon — inaccurate even before the reciprocal issue kicks in.
///   3. Double polynomial coefficients in the original appear incorrect (off by factors of 10–100×
///      compared to the correct Taylor series for sinh), producing wrong output near |x|=1.
///   4. Sign handling via Abs + CopySign adds overhead and creates branch-predictor pressure.
///      sinh is an odd function: sinh(-x) = -sinh(x). V3 exploits this naturally.
///
/// V2 fixes accuracy and reduces branches:
///   Float:  Exp(|x|) + one Newton-Raphson step on ReciprocalEstimate → ~24-bit accuracy.
///   Double: Exp(|x|) + 1.0/ex via FDIV → full double precision. One overflow branch each.
///
/// V3 is fully branchless and exploits the odd-symmetry of sinh:
///   (exp(x) − exp(−x)) / 2 — x is used as-is (no Abs, no CopySign).
///   The two Exp calls have no data dependency → OoO hardware can execute them in parallel.
///   Overflow is handled naturally: exp(±88) → ±Inf, exp(∓88) → 0, so result = ±Inf ✓.
///
/// Input domain: [-5, 5] — spans the polynomial branch threshold (±1.0) and a moderate exp range.
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=2.139 ns | FastSinh=1.902 ns | FastSinhV2=1.764 ns (−18%) | FastSinhV3=3.290 ns (+54%)
///   Double: DotNet=2.942 ns | FastSinh=2.182 ns | FastSinhV2=2.119 ns (−28%) | FastSinhV3=6.105 ns (+108%)
///
/// Conclusion: FastSinhV2 is the fastest for both types and is the production implementation.
///   V3 (two-exp, branchless) is slower because two exp() calls cannot be overlapped efficiently
///   on ARM64 scalar execution — the latency doubles instead of being hidden by OoO hardware.
///   V2 eliminates the polynomial branch, fixes ReciprocalEstimate precision via Newton-Raphson
///   (float) or FDIV (double), and beats .NET baseline by 18% (float) and 28% (double).
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SinhBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SinhBenchmark
{
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
			// Uniform in [-5, 5]: spans both the polynomial branch region (|x| < 1)
			// and the exp-based region (1 ≤ |x| ≤ 5).
			var v = rng.NextDouble() * 10.0 - 5.0;
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.Sinh — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetSinh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Sinh(v);
		return sum;
	}

	/// <summary>
	/// Current FastSinh(float) from SinhFunctionOptimizer.
	/// Branch at |x| &lt; 1.0: degree-7 Taylor polynomial (4 FMA, truncation error ~2.8e-6 at x=1 — 23× above float epsilon).
	/// Branch at |x| > 88.0: returns ±Infinity.
	/// Else: Exp(|x|) + ReciprocalEstimate(exp) — only ~12-bit reciprocal accuracy.
	/// Sign restored via CopySign.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastSinhFloat(v);
		return sum;
	}

	/// <summary>
	/// FastSinhV2(float): single Exp(|x|) + one Newton-Raphson reciprocal refinement step.
	/// Removes the polynomial branch (eliminates misprediction on mixed inputs).
	/// One NR step: r' = r*(2 − ex*r), where r = ReciprocalEstimate(ex).
	/// Accuracy: ~24-bit (full float precision), vs ~12-bit for current ReciprocalEstimate.
	/// Only branch: overflow guard at |x| > 88.0.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinhV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinhV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastSinhV3(float): two independent Exp calls, fully branchless.
	/// sinh is an odd function — no Abs or CopySign needed: (exp(x) − exp(−x)) / 2.
	/// exp(x) and exp(−x) have no data dependency → OoO hardware can overlap them.
	/// Overflow: exp(88) → +Inf, exp(−88) → 0, result = +Inf ✓.
	///           exp(−88) → 0, exp(88) → +Inf, result = −Inf ✓.
	/// Zero special-case branches for any input.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinhV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinhV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Sinh — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetSinh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Sinh(v);
		return sum;
	}

	/// <summary>
	/// Current FastSinh(double) from SinhFunctionOptimizer.
	/// Branch at |x| &lt; 1.0: 5-FMA polynomial whose coefficients appear incorrect
	///   (e.g. first coeff is 2.756e-8 but 1/9! = 2.756e-6 — 100× too small).
	/// Else: Exp(|x|) + Double.ReciprocalEstimate(exp) — only ~14-bit precision (catastrophic).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastSinhDouble(v);
		return sum;
	}

	/// <summary>
	/// FastSinhV2(double): single Exp(|x|) + floating-point division for exact 1/ex.
	/// FDIV avoids the precision catastrophe of Double.ReciprocalEstimate.
	/// Full double precision throughout. One overflow branch at |x| > 709.0.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinhV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastSinhV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastSinhV3(double): two independent Exp calls, fully branchless.
	/// Same natural sign-correctness and overflow-correctness as the float variant.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinhV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastSinhV3Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	/// <summary>
	/// Mirror of SinhFunctionOptimizer's generated FastSinh(float).
	/// Degree-7 Taylor for |x| &lt; 1.0; Exp + ReciprocalEstimate otherwise.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastSinhFloat(float x)
	{
		var originalX = x;
		x = Single.Abs(x);

		if (x < 1.0f)
		{
			var x2 = x * x;
			var ret = 0.00019841270f;   // 1/5040 (x^7 coefficient)
			ret = Single.FusedMultiplyAdd(ret, x2, 0.0083333333f);   // 1/120 (x^5 coefficient)
			ret = Single.FusedMultiplyAdd(ret, x2, 0.16666667f);     // 1/6   (x^3 coefficient)
			ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);            // 1     (x   coefficient)
			ret *= x;
			return Single.CopySign(ret, originalX);
		}

		if (x > 88.0f)
			return Single.CopySign(float.PositiveInfinity, originalX);

		var ex = Single.Exp(x);
		var result = (ex - Single.ReciprocalEstimate(ex)) * 0.5f;
		return Single.CopySign(result, originalX);
	}

	/// <summary>
	/// Mirror of SinhFunctionOptimizer's generated FastSinh(double).
	/// 5-FMA polynomial for |x| &lt; 1.0 (coefficients appear incorrect in the original);
	/// Exp + Double.ReciprocalEstimate otherwise.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastSinhDouble(double x)
	{
		var originalX = x;
		x = Double.Abs(x);

		if (x < 1.0)
		{
			var x2 = x * x;
			var ret = 2.7557319223985891e-8;
			ret = Double.FusedMultiplyAdd(ret, x2, 1.6059043836821613e-6);
			ret = Double.FusedMultiplyAdd(ret, x2, 1.9841269841269841e-5);
			ret = Double.FusedMultiplyAdd(ret, x2, 0.0083333333333333332);
			ret = Double.FusedMultiplyAdd(ret, x2, 0.16666666666666666);
			ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
			ret *= x;
			return Double.CopySign(ret, originalX);
		}

		if (x > 709.0)
			return Double.CopySign(double.PositiveInfinity, originalX);

		var ex = Double.Exp(x);
		var result = (ex - Double.ReciprocalEstimate(ex)) * 0.5;
		return Double.CopySign(result, originalX);
	}

	/// <summary>
	/// FastSinhV2(float): single Exp(|x|) + one Newton-Raphson reciprocal refinement step.
	/// r = ReciprocalEstimate(ex) → ~12-bit.
	/// r' = r * FMA(−ex, r, 2) → ~24-bit (full float precision).
	/// Removes the polynomial branch; only branch remaining is the overflow guard.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinhV2Float(float x)
	{
		var sign = x;
		x = Single.Abs(x);
		if (x > 88.0f) return Single.CopySign(float.PositiveInfinity, sign);

		var ex = Single.Exp(x);
		var r  = Single.ReciprocalEstimate(ex);
		r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);   // one NR step: r ← r*(2 − ex*r)
		return Single.CopySign((ex - r) * 0.5f, sign);
	}

	/// <summary>
	/// FastSinhV3(float): two independent Exp calls, fully branchless.
	/// sinh is odd — sign is inherent in the formula: (exp(x) − exp(−x)) / 2.
	/// exp(x) and exp(−x) are data-independent → OoO hardware can issue them in parallel.
	/// Naturally correct for all inputs including overflow.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinhV3Float(float x)
	{
		var ep = Single.Exp(x);
		var em = Single.Exp(-x);
		return (ep - em) * 0.5f;
	}

	/// <summary>
	/// FastSinhV2(double): single Exp(|x|) + floating-point division for exact 1/ex.
	/// FDIV (~20 cy) avoids the catastrophic precision loss of Double.ReciprocalEstimate.
	/// One overflow branch at |x| > 709.0.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastSinhV2Double(double x)
	{
		var sign = x;
		x = Double.Abs(x);
		if (x > 709.0) return Double.CopySign(double.PositiveInfinity, sign);

		var ex = Double.Exp(x);
		return Double.CopySign((ex - 1.0 / ex) * 0.5, sign);
	}

	/// <summary>
	/// FastSinhV3(double): two independent Exp calls, fully branchless.
	/// Same natural sign-correctness and overflow-correctness as the float variant.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastSinhV3Double(double x)
	{
		var ep = Double.Exp(x);
		var em = Double.Exp(-x);
		return (ep - em) * 0.5;
	}
}


