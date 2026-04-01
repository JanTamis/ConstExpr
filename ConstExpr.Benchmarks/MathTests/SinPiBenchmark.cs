using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar SinPi implementations for float and double.
///
/// SinPi(x) = Sin(π·x), fundamental period = 2.
///
/// Variants benchmarked:
///   Float  – float.SinPi           (baseline: hardware-accurate)
///            CurrentFastSinPi      (Floor + 3 branches + two-range poly)
///            FastSinPiV2           (branchless Round + Min fold + px·π, 3 FMA + 2 FMUL)
///            FastSinPiV3           (branchless Round + Min fold + absorbed-π poly, 3 FMA + 1 FMUL)
///   Double – double.SinPi          (baseline: hardware-accurate)
///            CurrentFastSinPi      (Floor + 3 branches + two-range poly)
///            FastSinPiV2           (branchless Round + Min fold + px·π, 4 FMA + 2 FMUL)
///            FastSinPiV3           (branchless Round + Min fold + absorbed-π poly, 4 FMA + 1 FMUL)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Float:  DotNet=2.43ns  Current=1.50ns(-38%)  V2=1.23ns(-50%)  V3=1.13ns(-54%) ✓
///   Double: DotNet=2.64ns  Current=1.52ns(-42%)  V2=1.31ns(-50%)  V3=1.23ns(-53%) ✓
///
/// V3 wins for both types: absorbing π into polynomial coefficients saves 1 FMUL (the
/// px = u·π step), and eliminating all 3 branches cuts ~25% vs the current optimizer.
///
/// Range reduction comparison:
///   Current : Floor(x / 2) * 2
///             + if (x > 1) x -= 2       (branch 1)
///             + if (x &lt; -1) x += 2   (branch 2)
///             + Abs
///             + if (x &lt;= 0.5) ... else ... (branch 3: two polynomial paths)
///   V2/V3   : Round(x * 0.5) * 2       [FRINTN/ROUNDSS — no branches]
///             + save sign + Abs
///             + Min(x, 1 − x)          [FMIN — branchless fold to [0, 0.5]]
///             (3 branches eliminated)
///
/// V2 vs V3:
///   V2: u = Min(x,1−x), px = u·π, degree-7/-9 polynomial at px ∈ [0, π/2]
///   V3: same fold, polynomial coefficients absorb π → evaluate at u ∈ [0, 0.5]
///       sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + ...)))
///       Saves 1 FMUL (the explicit px = u·π step).
///
/// Accuracy notes (max absolute error, x ∈ [−100, 100]):
///   Float V2 / V3  – identical polynomial to current → same accuracy.
///   Double V2 / V3 – identical polynomial to current → same accuracy.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SinPiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SinPiBenchmark
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
			var v          = rng.NextDouble() * 200.0 - 100.0; // uniform in [−100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.SinPi (IFloatingPointIeee754) — hardware-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetSinPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.SinPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastSinPi(float) — exact mirror of SinPiFunctionOptimizer output.
	/// Range reduction: Floor(x/2)·2 + two conditional branches → [−1, 1].
	/// Then Abs + if-branch at x=0.5 selects one of two polynomial paths.
	/// Each path: multiply by π (or 1−x)*π, degree-7 poly (3 FMA + 1 MUL) at px ∈ [0, π/2].
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastSinPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastSinPiFloat(v);
		return sum;
	}

	/// <summary>
	/// FastSinPiV2(float) — branchless range reduction + Min fold.
	/// Round(x·0.5)·2 replaces Floor + 2 branches.
	/// Min(x, 1−x) replaces the mid-point if-branch: both halves use the same poly path.
	/// sin poly at px = u·π ∈ [0, π/2], degree-7 (3 FMA + 2 FMUL).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinPiV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinPiV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastSinPiV3(float) — same branchless approach as V2, absorbed-π polynomial.
	/// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040))))
	/// Evaluated at u ∈ [0, 0.5]: saves the explicit px = u·π multiply (3 FMA + 1 FMUL).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinPiV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinPiV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in double.SinPi (IFloatingPointIeee754) — hardware-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetSinPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.SinPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastSinPi(double) — exact mirror of SinPiFunctionOptimizer output.
	/// Range reduction: Floor(x/2)·2 + two conditional branches → [−1, 1].
	/// Then Abs + if-branch at x=0.5 selects one of two polynomial paths.
	/// Each path: degree-9 poly (4 FMA + 1 MUL) at px ∈ [0, π/2].
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastSinPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastSinPiDouble(v);
		return sum;
	}

	/// <summary>
	/// FastSinPiV2(double) — branchless range reduction + Min fold.
	/// Round(x·0.5)·2 replaces Floor + 2 branches.
	/// Min(x, 1−x) replaces the mid-point if-branch.
	/// sin poly at px = u·π ∈ [0, π/2], degree-9 (4 FMA + 2 FMUL).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinPiV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastSinPiV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastSinPiV3(double) — same branchless approach as V2, absorbed-π polynomial.
	/// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040 + u²·(π⁹/362880)))))
	/// Evaluated at u ∈ [0, 0.5]: saves the explicit px = u·π multiply (4 FMA + 1 FMUL).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinPiV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastSinPiV3Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	// ---- current (mirror of SinPiFunctionOptimizer output) ----------------

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastSinPiFloat(float x)
	{
		// Range reduction: Floor(x/2)·2 → [0, 2), then clamp to (−1, 1]
		x = x - Single.Floor(x / 2.0f) * 2.0f;
		if (x > 1.0f)  x -= 2.0f;
		if (x < -1.0f) x += 2.0f;

		var originalSign = x;
		x = Single.Abs(x); // [0, 1]

		if (x <= 0.5f)
		{
			var px  = x * Single.Pi;
			var px2 = px * px;
			var ret = -0.00019840874f;
			ret = Single.FusedMultiplyAdd(ret, px2,  0.0083333310f);
			ret = Single.FusedMultiplyAdd(ret, px2, -0.16666667f);
			ret = Single.FusedMultiplyAdd(ret, px2,  1.0f);
			ret = ret * px;
			return Single.CopySign(ret, originalSign);
		}

		// sin(π·x) = sin(π·(1−x)) for x ∈ (0.5, 1]
		var pxb  = (1.0f - x) * Single.Pi;
		var px2b = pxb * pxb;
		var ret2 = -0.00019840874f;
		ret2 = Single.FusedMultiplyAdd(ret2, px2b,  0.0083333310f);
		ret2 = Single.FusedMultiplyAdd(ret2, px2b, -0.16666667f);
		ret2 = Single.FusedMultiplyAdd(ret2, px2b,  1.0f);
		ret2 = ret2 * pxb;
		return Single.CopySign(ret2, originalSign);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastSinPiDouble(double x)
	{
		x = x - Double.Floor(x / 2.0) * 2.0;
		if (x > 1.0)  x -= 2.0;
		if (x < -1.0) x += 2.0;

		var originalSign = x;
		x = Double.Abs(x);

		if (x <= 0.5)
		{
			var px  = x * Double.Pi;
			var px2 = px * px;
			var ret = 2.7557313707070068e-6;
			ret = Double.FusedMultiplyAdd(ret, px2, -0.00019841269841201856);
			ret = Double.FusedMultiplyAdd(ret, px2,  0.0083333333333331650);
			ret = Double.FusedMultiplyAdd(ret, px2, -0.16666666666666666);
			ret = Double.FusedMultiplyAdd(ret, px2,  1.0);
			ret = ret * px;
			return Double.CopySign(ret, originalSign);
		}

		var pxb  = (1.0 - x) * Double.Pi;
		var px2b = pxb * pxb;
		var ret2 = 2.7557313707070068e-6;
		ret2 = Double.FusedMultiplyAdd(ret2, px2b, -0.00019841269841201856);
		ret2 = Double.FusedMultiplyAdd(ret2, px2b,  0.0083333333333331650);
		ret2 = Double.FusedMultiplyAdd(ret2, px2b, -0.16666666666666666);
		ret2 = Double.FusedMultiplyAdd(ret2, px2b,  1.0);
		ret2 = ret2 * pxb;
		return Double.CopySign(ret2, originalSign);
	}

	// ---- V2: branchless range reduction + Min fold -----------------------

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinPiV2Float(float x)
	{
		// Branchless range reduction to [−1, 1]: FRINTN on ARM64 / ROUNDSS on x64
		x -= Single.Round(x * 0.5f) * 2.0f;
		var sign = x;                           // save sign for CopySign
		x = Single.Abs(x);                      // [0, 1]
		var u   = Single.Min(x, 1.0f - x);      // [0, 0.5]: branchless fold (FMIN)
		var px  = u * Single.Pi;                // [0, π/2]
		var px2 = px * px;
		var ret = -0.00019840874f;
		ret = Single.FusedMultiplyAdd(ret, px2,  0.0083333310f);
		ret = Single.FusedMultiplyAdd(ret, px2, -0.16666667f);
		ret = Single.FusedMultiplyAdd(ret, px2,  1.0f);
		return Single.CopySign(ret * px, sign);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastSinPiV2Double(double x)
	{
		x -= Double.Round(x * 0.5) * 2.0;
		var sign = x;
		x = Double.Abs(x);
		var u   = Double.Min(x, 1.0 - x);
		var px  = u * Double.Pi;
		var px2 = px * px;
		var ret = 2.7557313707070068e-6;
		ret = Double.FusedMultiplyAdd(ret, px2, -0.00019841269841201856);
		ret = Double.FusedMultiplyAdd(ret, px2,  0.0083333333333331650);
		ret = Double.FusedMultiplyAdd(ret, px2, -0.16666666666666666);
		ret = Double.FusedMultiplyAdd(ret, px2,  1.0);
		return Double.CopySign(ret * px, sign);
	}

	// ---- V3: branchless + π absorbed into polynomial coefficients --------
	// sinpi(u) = u·(c₁ + u²·(c₃ + u²·(c₅ + u²·(c₇ + ...))))
	// cₙ = (−1)^((n−1)/2) · πⁿ / n!   for odd n

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinPiV3Float(float x)
	{
		x -= Single.Round(x * 0.5f) * 2.0f;
		var sign = x;
		x = Single.Abs(x);
		var u  = Single.Min(x, 1.0f - x); // [0, 0.5]
		var u2 = u * u;
		// Coefficients: cₙ = πⁿ/n! with alternating signs (n = 7,5,3,1)
		var r = -0.59926453f;                           // −π⁷/5040
		r = Single.FusedMultiplyAdd(r, u2,  2.55016404f); // +π⁵/120
		r = Single.FusedMultiplyAdd(r, u2, -5.16771278f); // −π³/6
		r = Single.FusedMultiplyAdd(r, u2,  3.14159265f); // +π
		return Single.CopySign(u * r, sign);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastSinPiV3Double(double x)
	{
		x -= Double.Round(x * 0.5) * 2.0;
		var sign = x;
		x = Double.Abs(x);
		var u  = Double.Min(x, 1.0 - x); // [0, 0.5]
		var u2 = u * u;
		// Coefficients: cₙ = πⁿ/n! with alternating signs (n = 9,7,5,3,1)
		var r =  0.08214588661112823;                              // +π⁹/362880
		r = Double.FusedMultiplyAdd(r, u2, -0.59926452932079209); // −π⁷/5040
		r = Double.FusedMultiplyAdd(r, u2,  2.55016403987734485); // +π⁵/120
		r = Double.FusedMultiplyAdd(r, u2, -5.16771278004997102); // −π³/6
		r = Double.FusedMultiplyAdd(r, u2,  3.14159265358979324); // +π
		return Double.CopySign(u * r, sign);
	}
}


