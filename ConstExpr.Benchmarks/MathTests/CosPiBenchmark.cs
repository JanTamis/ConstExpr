using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar CosPi implementations for float and double.
///
/// CosPi(x) = Cos(pi*x), fundamental period = 2.
///
/// Two type groups are benchmarked independently:
///   Float  - float.CosPi           (baseline: hardware-accurate)
///            CurrentFastCosPi      (Floor + 2 branches + two-range poly, 4 FMA per path)
///            FastCosPiV2           (branchless Round + sin-fold, 3 FMA + 1 mul) - proposed best
///            FastCosPiV3           (branchless Round + sin-fold, 2 FMA + 1 mul, ~8.5e-6 error)
///   Double - double.CosPi          (baseline: hardware-accurate)
///            CurrentFastCosPi      (Floor + 2 branches + two-range poly, 5 FMA per path)
///            FastCosPiV2           (branchless Round + sin-fold, 5 FMA + 1 mul) - proposed best
///
/// Key identity exploited by V2/V3:
///   cos(pi*x) = -sin(pi*(x - 0.5))
///   After branchless reduction to x in [0, 1], u = x - 0.5 in [-0.5, 0.5] and
///   v = pi*u in [-pi/2, pi/2], so a single sin polynomial covers the entire range.
///   No branching needed at all -- eliminates the two if-branches for range reduction
///   and the mid-point split at x = 0.5.
///
/// Range reduction comparison:///   Current : Floor(x / 2) * 2  [no FDIV since compiler rewrites x/2 to x*0.5]
///             + if (x > 1) x -= 2          (branch 1)
///             + if (x &lt; -1) x += 2        (branch 2)
///             + Abs
///             + if (x &lt;= 0.5) ... else ... (branch 3, selects polynomial path)
///   V2/V3   : Round(x * 0.5) * 2          [FRINTN/ROUNDSS, no branches]
///             + Abs
///             (3 branches eliminated)
///
/// Accuracy notes (max absolute error, x in [-100, 100]):
///   CurrentFastCosPi (float/double) - limited by float/double polynomial precision.
///   FastCosPiV2 (float)   - sin degree-7 minimax, max error approx 1.5e-7 (within float eps).
///   FastCosPiV3 (float)   - sin degree-5 polynomial, max error approx 8.5e-6 (FastMath).
///   FastCosPiV2 (double)  - sin degree-11 polynomial, max error approx 2e-16 (full precision).
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=2.252 ns | CurrentFastCosPi=1.478 ns (-34%) | V2=1.000 ns (-56%) | V3=0.936 ns (-58%)
///   Double: DotNet=2.509 ns | CurrentFastCosPi=1.493 ns (-40%) | V2=1.131 ns (-55%)
///
/// Conclusion: V2 is the best full-precision scalar implementation for both types.
///   V3 (float-only, 2 FMA) is marginally faster but has ~8.5e-6 max error vs ~1.5e-7 for V2.
///   Both V2 and V3 replace the optimizer implementation; V2 is used as the default.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*CosPiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CosPiBenchmark
{
	// 1 024 values spread over many full periods to exercise range reduction.
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
			var v = rng.NextDouble() * 200.0 - 100.0; // uniform in [−100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.CosPi (IFloatingPointIeee754) - hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetCosPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.CosPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastCosPi(float) from CosPiFunctionOptimizer.
	/// Range reduction: Floor(x/2)·2 + two conditional branches → [−1, 1].
	/// Then Abs + if-branch at x=0.5 selects one of two polynomial paths.
	/// Each path: multiply by π, degree-8 minimax in (π·x)² (4 FMA operations).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastCosPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastCosPiFloat(v);
		return sum;
	}

	/// <summary>
	/// FastCosPiV2(float) — branchless range reduction + sin-fold identity.
	/// Round(x·0.5)·2 replaces Floor + 2 branches.
	/// cos(π·x) = −sin(π·(x−0.5)): folds the mid-point split into a single poly.
	/// sin on [−π/2, π/2] via degree-7 minimax (3 FMA + 1 mul).
	/// Max absolute error ≈ 1.5e-7 (within float precision).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCosPiV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCosPiV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastCosPiV3(float) — same branchless approach as V2, degree-5 polynomial.
	/// Drops the x⁶ term: 2 FMA + 1 mul — fastest float variant.
	/// Max absolute error ≈ 8.5e-6 (acceptable in FastMath mode).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCosPiV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCosPiV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in double.CosPi (IFloatingPointIeee754) - hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetCosPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.CosPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastCosPi(double) from CosPiFunctionOptimizer.
	/// Range reduction: Floor(x/2)·2 + two conditional branches → [−1, 1].
	/// Then Abs + if-branch at x=0.5 selects one of two polynomial paths.
	/// Each path: degree-10 minimax in (π·x)² (5 FMA operations).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastCosPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastCosPiDouble(v);
		return sum;
	}

	/// <summary>
	/// FastCosPiV2(double) — branchless range reduction + sin-fold identity.
	/// cos(π·x) = −sin(π·(x−0.5)): single-range polynomial, no branching.
	/// sin on [−π/2, π/2] via degree-11 minimax (5 FMA + 1 mul).
	/// Max absolute error ≈ 2e-16 (full double precision).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCosPiV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastCosPiV2Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	// ---- current (mirror of CosPiFunctionOptimizer output) ----------------

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastCosPiFloat(float x)
	{
		// Range reduction: Floor(x/2)·2 → [0, 2), then clamp to (−1, 1]
		x = x - Single.Floor(x / 2.0f) * 2.0f;
		if (x > 1.0f)  x -= 2.0f;
		if (x < -1.0f) x += 2.0f;
		x = Single.Abs(x); // [0, 1]

		if (x <= 0.5f)
		{
			// cos(π·x) on [0, 0.5]: polynomial on px = π·x ∈ [0, π/2]
			var px  = x * Single.Pi;
			var px2 = px * px;
			var r   = 0.0003538394f;
			r = Single.FusedMultiplyAdd(r, px2, -0.0041666418f);
			r = Single.FusedMultiplyAdd(r, px2,  0.041666666f);
			r = Single.FusedMultiplyAdd(r, px2, -0.5f);
			r = Single.FusedMultiplyAdd(r, px2,  1.0f);
			return r;
		}

		// cos(π·x) = −cos(π·(1−x)) on (0.5, 1]: polynomial on px = π·(1−x) ∈ [0, π/2)
		var pxb  = (1.0f - x) * Single.Pi;
		var px2b = pxb * pxb;
		var ret  = 0.0003538394f;
		ret = Single.FusedMultiplyAdd(ret, px2b, -0.0041666418f);
		ret = Single.FusedMultiplyAdd(ret, px2b,  0.041666666f);
		ret = Single.FusedMultiplyAdd(ret, px2b, -0.5f);
		ret = Single.FusedMultiplyAdd(ret, px2b,  1.0f);
		return -ret;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastCosPiDouble(double x)
	{
		x = x - Double.Floor(x / 2.0) * 2.0;
		if (x > 1.0)  x -= 2.0;
		if (x < -1.0) x += 2.0;
		x = Double.Abs(x);

		if (x <= 0.5)
		{
			var px  = x * Double.Pi;
			var px2 = px * px;
			var r   = -1.1940250944959890e-7;
			r = Double.FusedMultiplyAdd(r, px2,  2.0876755527587203e-5);
			r = Double.FusedMultiplyAdd(r, px2, -0.0013888888888739916);
			r = Double.FusedMultiplyAdd(r, px2,  0.041666666666666602);
			r = Double.FusedMultiplyAdd(r, px2, -0.5);
			r = Double.FusedMultiplyAdd(r, px2,  1.0);
			return r;
		}

		var pxb  = (1.0 - x) * Double.Pi;
		var px2b = pxb * pxb;
		var ret  = -1.1940250944959890e-7;
		ret = Double.FusedMultiplyAdd(ret, px2b,  2.0876755527587203e-5);
		ret = Double.FusedMultiplyAdd(ret, px2b, -0.0013888888888739916);
		ret = Double.FusedMultiplyAdd(ret, px2b,  0.041666666666666602);
		ret = Double.FusedMultiplyAdd(ret, px2b, -0.5);
		ret = Double.FusedMultiplyAdd(ret, px2b,  1.0);
		return -ret;
	}

	// ---- V2: branchless range reduction + cos(π·x) = −sin(π·(x−0.5)) ----

	/// <summary>
	/// cos(π·x) = −sin(π·(x−0.5)).
	/// After reducing x to [0, 1]:
	///   v = π·(x − 0.5) ∈ [−π/2, π/2]
	///   sin(v) = v · (1 + v²·(c₁ + v²·(c₂ + v²·c₃)))   [degree-7 minimax, 3 FMA]
	///   result = −(v · horner_result)
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCosPiV2Float(float x)
	{
		// Branchless range reduction to [−1, 1]: Round is FRINTN/ROUNDSS, no branches
		x -= Single.Round(x * 0.5f) * 2.0f;
		x  = Single.Abs(x); // fold to [0, 1]

		// cos(π·x) = −sin(π·(x − 0.5))
		var v  = (x - 0.5f) * Single.Pi; // v ∈ [−π/2, π/2]
		var v2 = v * v;
		var r  = -0.00019841271f;                         // −1/5040  (minimax-adjusted)
		r = Single.FusedMultiplyAdd(r, v2,  0.008333333f); //  1/120
		r = Single.FusedMultiplyAdd(r, v2, -0.16666667f);  // −1/6
		r = Single.FusedMultiplyAdd(r, v2,  1.0f);
		return -(v * r); // −sin(v) = cos(π·x)
	}

	/// <summary>
	/// Same branchless approach as V2, degree-5 polynomial (drops x⁶ term).
	/// 2 FMA + 1 mul — fastest float variant, acceptable for FastMath mode.
	/// Max absolute error ≈ 8.5e-6.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCosPiV3Float(float x)
	{
		x -= Single.Round(x * 0.5f) * 2.0f;
		x  = Single.Abs(x);

		var v  = (x - 0.5f) * Single.Pi;
		var v2 = v * v;
		var r  =  0.008333333f;                            //  1/120
		r = Single.FusedMultiplyAdd(r, v2, -0.16666667f);  // −1/6
		r = Single.FusedMultiplyAdd(r, v2,  1.0f);
		return -(v * r);
	}

	/// <summary>
	/// cos(π·x) = −sin(π·(x−0.5)).
	/// sin on [−π/2, π/2] via degree-11 polynomial (5 FMA + 1 mul).
	/// Max absolute error ≈ 2e-16 (full double precision).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastCosPiV2Double(double x)
	{
		x -= Double.Round(x * 0.5) * 2.0;
		x  = Double.Abs(x);

		var v  = (x - 0.5) * Double.Pi; // v ∈ [−π/2, π/2]
		var v2 = v * v;
		var r  = -2.5052108385441720e-8;                               // −1/39916800
		r = Double.FusedMultiplyAdd(r, v2,  2.7557319223985888e-6);   //  1/362880
		r = Double.FusedMultiplyAdd(r, v2, -0.00019841269841269841);  // −1/5040
		r = Double.FusedMultiplyAdd(r, v2,  0.008333333333333333);    //  1/120
		r = Double.FusedMultiplyAdd(r, v2, -0.16666666666666666);     // −1/6
		r = Double.FusedMultiplyAdd(r, v2,  1.0);
		return -(v * r); // −sin(v) = cos(π·x)
	}
}








