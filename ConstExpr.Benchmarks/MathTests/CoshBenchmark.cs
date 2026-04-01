using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Cosh implementations for float and double.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Cosh  vs FastCosh(float)   [current: branch at 1.0 + polynomial, or exp + ReciprocalEstimate]
///                         vs FastCoshV2(float) [single Exp + Newton-Raphson refined reciprocal, overflow branch only]
///                         vs FastCoshV3(float) [two independent Exp calls, fully branchless]
///   Double – Math.Cosh   vs FastCosh(double)  [current: branch at 1.0 + polynomial, or exp + ReciprocalEstimate]
///                         vs FastCoshV2(double)[single Exp + floating-point division 1/exp, overflow branch only]
///                         vs FastCoshV3(double)[two independent Exp calls, fully branchless]
///
/// Issues found in the current implementation:
///   1. Single.ReciprocalEstimate / Double.ReciprocalEstimate gives only ~12/14-bit precision.
///      For cosh(1.0f): absolute error ≈ 4.5e-5 (vs float epsilon ~1.2e-7) — ~375× too large.
///      For cosh(1.0):  absolute error ≈ 5.5e-5 (vs double epsilon ~2.2e-16) — catastrophically bad.
///   2. The float polynomial uses only degree-6 (3 FMA steps); the truncation error at x=1 is
///      x^8/8! ≈ 2.5e-5, which is ~200× above float precision.
///   3. Two branches (&lt;1.0 and &gt;88.0) add branch-prediction overhead on mixed-value inputs.
///
/// V2 fixes accuracy and eliminates the polynomial branch:
///   Float:  exp + one Newton-Raphson reciprocal step restores ~24-bit accuracy — no branch cost.
///   Double: exp + fp division for exact 1/exp — avoids two-exp overhead for typical inputs.
///
/// V3 is fully branchless and maximally accurate:
///   Both exp(x) and exp(-x) are computed independently; out-of-order hardware can overlap them.
///   Overflow is handled naturally: exp(x) → +Inf, exp(-x) → 0, so (Inf + 0)*0.5 = Inf ✓.
///   No special-case branch needed.
///
/// Input domain: [-5, 5] — spans the polynomial branch threshold (±1.0) and a moderate exp range.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*CoshBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CoshBenchmark
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

	/// <summary>Built-in MathF.Cosh — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetCosh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Cosh(v);
		return sum;
	}

	/// <summary>
	/// Current FastCosh(float) from CoshFunctionOptimizer.
	/// Branch at |x| &lt; 1.0: degree-6 Taylor polynomial (3 FMA, truncation error ~2.5e-5 at x=1).
	/// Branch at |x| > 88.0: returns +Infinity.
	/// Else: exp(|x|) + ReciprocalEstimate(exp(|x|)) — only ~12-bit reciprocal accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCosh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastCoshFloat(v);
		return sum;
	}

	/// <summary>
	/// FastCoshV2(float): single Exp + one Newton-Raphson reciprocal refinement step.
	/// Removes the polynomial branch (eliminates branch misprediction on mixed inputs).
	/// One NR step: r' = r*(2 − exp(x)*r), where r = ReciprocalEstimate(exp(x)).
	/// Accuracy: ~24-bit (full float precision), vs ~12-bit for current ReciprocalEstimate.
	/// Only branch: overflow guard at |x| > 88.0.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCoshV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCoshV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastCoshV3(float): two independent Exp calls, fully branchless.
	/// exp(|x|) and exp(-|x|) have no data dependency → out-of-order hardware can overlap them.
	/// Overflow: exp(|x|) → +Inf, exp(-|x|) → 0, so (Inf + 0)*0.5 = Inf — naturally correct.
	/// No special-case branch needed for any input.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCoshV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCoshV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Cosh — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetCosh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Cosh(v);
		return sum;
	}

	/// <summary>
	/// Current FastCosh(double) from CoshFunctionOptimizer.
	/// Branch at |x| &lt; 1.0: degree-10 polynomial (6 FMA steps).
	/// Else: exp(|x|) + Double.ReciprocalEstimate(exp) — only ~14-bit precision (catastrophic for double).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCosh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastCoshDouble(v);
		return sum;
	}

	/// <summary>
	/// FastCoshV2(double): single Exp + floating-point division for 1/exp(x).
	/// Division (FDIV) avoids the accuracy issue of ReciprocalEstimate.
	/// Full double precision throughout. One overflow branch at |x| > 709.0.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCoshV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastCoshV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastCoshV3(double): two independent Exp calls, fully branchless.
	/// Same natural overflow/underflow correctness as FastCoshV3(float).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCoshV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastCoshV3Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	/// <summary>
	/// Mirror of CoshFunctionOptimizer's generated FastCosh(float).
	/// Degree-6 Taylor for |x| &lt; 1.0; exp + ReciprocalEstimate otherwise.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastCoshFloat(float x)
	{
		x = Single.Abs(x);

		if (x < 1.0f)
		{
			var x2 = x * x;
			var ret = 0.0013888889f;                                    // 1/6!
			ret = Single.FusedMultiplyAdd(ret, x2, 0.041666667f);      // 1/4!
			ret = Single.FusedMultiplyAdd(ret, x2, 0.5f);              // 1/2!
			ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);              // 1
			return ret;
		}

		if (x > 88.0f) return float.PositiveInfinity;

		var ex = Single.Exp(x);
		return (ex + Single.ReciprocalEstimate(ex)) * 0.5f;
	}

	/// <summary>
	/// Mirror of CoshFunctionOptimizer's generated FastCosh(double).
	/// Degree-10 polynomial for |x| &lt; 1.0; exp + ReciprocalEstimate otherwise.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastCoshDouble(double x)
	{
		x = Double.Abs(x);

		if (x < 1.0)
		{
			var x2 = x * x;
			var ret = 2.0876756987868099e-8;
			ret = Double.FusedMultiplyAdd(ret, x2, 2.4801587301587302e-7);
			ret = Double.FusedMultiplyAdd(ret, x2, 0.0013888888888888889);
			ret = Double.FusedMultiplyAdd(ret, x2, 0.041666666666666664);
			ret = Double.FusedMultiplyAdd(ret, x2, 0.5);
			ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
			return ret;
		}

		if (x > 709.0) return double.PositiveInfinity;

		var ex = Double.Exp(x);
		return (ex + Double.ReciprocalEstimate(ex)) * 0.5;
	}

	/// <summary>
	/// FastCoshV2(float): single Exp + one Newton-Raphson reciprocal refinement step.
	/// r = ReciprocalEstimate(ex) → ~12-bit.
	/// r' = r * FMA(-ex, r, 2) → ~24-bit (full float precision).
	/// Removes the polynomial branch; only branch remaining is the overflow guard.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCoshV2Float(float x)
	{
		x = Single.Abs(x);
		if (x > 88.0f) return float.PositiveInfinity;

		var ex = Single.Exp(x);
		var r  = Single.ReciprocalEstimate(ex);
		r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);   // one NR step: r ← r*(2 − ex*r)
		return (ex + r) * 0.5f;
	}

	/// <summary>
	/// FastCoshV3(float): two independent Exp calls, fully branchless.
	/// exp(|x|) and exp(-|x|) are data-independent → OoO hardware can issue them in parallel.
	/// Naturally correct for overflow (exp overflows to +Inf, exp(-x) underflows to 0).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCoshV3Float(float x)
	{
		x  = Single.Abs(x);
		var em = Single.Exp(-x);
		var ep = Single.Exp(x);
		return (ep + em) * 0.5f;
	}

	/// <summary>
	/// FastCoshV2(double): single Exp + floating-point division for exact 1/exp(x).
	/// FDIV is slow (~20 cy) but avoids the catastrophic precision loss of ReciprocalEstimate.
	/// Single overflow branch at |x| > 709.0.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastCoshV2Double(double x)
	{
		x = Double.Abs(x);
		if (x > 709.0) return double.PositiveInfinity;

		var ex = Double.Exp(x);
		return (ex + 1.0 / ex) * 0.5;
	}

	/// <summary>
	/// FastCoshV3(double): two independent Exp calls, fully branchless.
	/// Same natural overflow correctness as the float variant.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastCoshV3Double(double x)
	{
		x  = Double.Abs(x);
		var em = Double.Exp(-x);
		var ep = Double.Exp(x);
		return (ep + em) * 0.5;
	}
}

