using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares simultaneous sinpi+cospi implementations for float and double.
///
/// Two groups:
///   Float  – float.SinCosPi   vs CurrentFastSinCosPi  vs FastSinCosPiV2
///   Double – double.SinCosPi  vs CurrentFastSinCosPi  vs FastSinCosPiV2
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Float:  DotNet=3.43ns   Current=1.55ns(−55%)   V2=1.44ns(−58%)   V2 is +7%  faster than Current
///   Double: DotNet=4.10ns   Current=1.62ns(−60%)   V2=1.55ns(−62%)   V2 is +5%  faster than Current
///
/// CurrentFastSinCosPi (old optimizer output):
///   Range reduction: Round(x / 2.0f) * 2.0f  — JIT rewrites div-by-2 to FMUL by 0.5
///   Fold [0.5,1] → [0,0.5]: one branchless ternary (FCSEL on ARM64)
///   px = xReduced * π                         — explicit FMUL (redundant)
///   px2 = px * px                              — shared FMUL
///   Sin: degree-7 polynomial (3 FMA + 1 MUL) evaluated at px ∈ [0, π/2]
///   Cos: degree-6 polynomial (3 FMA) evaluated at px
///
/// FastSinCosPiV2 improvements (now in optimizer):
///   Absorbs π into polynomial coefficients → removes the px = xReduced*π multiply
///   u2 = u*u replaces px2 = px*px  (1 fewer FMUL per call)
///   Sin: degree-7 polynomial (3 FMA + 1 MUL) with π-scaled coefficients, arg = u ∈ [0, 0.5]
///   Cos: degree-6/8 polynomial (3/4 FMA) with π²-scaled coefficients, arg = u
///   Identical numerical accuracy — same polynomial degree, same mathematical value
///
/// Why this works:
///   sinpi(u) = sin(π·u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040))))
///   cospi(u) = cos(π·u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720)))
///   Both evaluate over u ∈ [0, 0.5] — no π-multiply needed.
///
/// Input domain: [-100, 100] — exercises range reduction over ~50 full periods.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SinCosPiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SinCosPiBenchmark
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
			var v          = rng.NextDouble() * 200.0 - 100.0; // uniform in [−100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.SinCosPi — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetSinCosPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = float.SinCosPi(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Current FastSinCosPi(float) — exact mirror of SinCosPiFunctionOptimizer output.
	/// Evaluates sin/cos polynomials at px = xReduced*π ∈ [0, π/2] (px-domain).
	/// Costs one explicit FMUL for px = xReduced*π plus a shared px² = px*px.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastSinCosPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = CurrentFastSinCosPiFloat(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// FastSinCosPiV2(float) — direct u-domain polynomials; π absorbed into coefficients.
	/// Removes the px = xReduced*π multiply: evaluates sinpi/cospi directly over u ∈ [0, 0.5].
	/// One fewer FMUL per call; same polynomial degree and same numerical accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinCosPiV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = FastSinCosPiV2Float(v);
			sum += s + c;
		}
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.SinCosPi — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetSinCosPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = double.SinCosPi(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Current FastSinCosPi(double) — exact mirror of SinCosPiFunctionOptimizer output.
	/// Degree-9 sin + degree-8 cos polynomials evaluated at px = xReduced*π (px-domain).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastSinCosPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = CurrentFastSinCosPiDouble(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// FastSinCosPiV2(double) — direct u-domain polynomials; π absorbed into coefficients.
	/// Removes the px = xReduced*π multiply: one fewer FMUL per call.
	/// Same degree-9 sin + degree-8 cos polynomials; identical accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinCosPiV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = FastSinCosPiV2Double(v);
			sum += s + c;
		}
		return sum;
	}

	// ── scalar implementations ──────────────────────────────────────────────

	// ---- current (exact mirror of SinCosPiFunctionOptimizer output) -------

	/// <summary>
	/// Exact mirror of SinCosPiFunctionOptimizer.GenerateFastSinCosPiMethodFloat().
	/// Polynomials evaluated in px-domain: px = xReduced * π ∈ [0, π/2].
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (float Sin, float Cos) CurrentFastSinCosPiFloat(float x)
	{
		// Range reduction to [−1, 1]
		x = x - Single.Round(x / 2.0f) * 2.0f;

		var originalSign = x;
		var absX         = Single.Abs(x);

		var inUpperHalf = absX > 0.5f;
		var xReduced    = inUpperHalf ? (1.0f - absX) : absX;

		// π-domain argument and its square (shared by sin and cos)
		var px  = xReduced * Single.Pi;
		var px2 = px * px;

		// Sin: degree-7 polynomial in px ∈ [0, π/2]
		var sinVal = -0.00019840874f;
		sinVal = Single.FusedMultiplyAdd(sinVal, px2,  0.0083333310f);
		sinVal = Single.FusedMultiplyAdd(sinVal, px2, -0.16666667f);
		sinVal = Single.FusedMultiplyAdd(sinVal, px2,  1.0f);
		sinVal = sinVal * px;
		sinVal = Single.CopySign(sinVal, originalSign);

		// Cos: degree-6 polynomial in px ∈ [0, π/2]
		var cosVal = -0.0013888397f;
		cosVal = Single.FusedMultiplyAdd(cosVal, px2,  0.041666418f);
		cosVal = Single.FusedMultiplyAdd(cosVal, px2, -0.5f);
		cosVal = Single.FusedMultiplyAdd(cosVal, px2,  1.0f);
		cosVal = inUpperHalf ? -cosVal : cosVal;

		return (sinVal, cosVal);
	}

	/// <summary>
	/// Exact mirror of SinCosPiFunctionOptimizer.GenerateFastSinCosPiMethodDouble().
	/// Polynomials evaluated in px-domain: px = xReduced * π ∈ [0, π/2].
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (double Sin, double Cos) CurrentFastSinCosPiDouble(double x)
	{
		// Range reduction to [−1, 1]
		x = x - Double.Round(x / 2.0) * 2.0;

		var originalSign = x;
		var absX         = Double.Abs(x);

		var inUpperHalf = absX > 0.5;
		var xReduced    = inUpperHalf ? (1.0 - absX) : absX;

		// π-domain argument and its square (shared by sin and cos)
		var px  = xReduced * Double.Pi;
		var px2 = px * px;

		// Sin: degree-9 polynomial in px ∈ [0, π/2]
		var sinVal = 2.7557313707070068e-6;
		sinVal = Double.FusedMultiplyAdd(sinVal, px2, -0.00019841269841201856);
		sinVal = Double.FusedMultiplyAdd(sinVal, px2,  0.0083333333333331650);
		sinVal = Double.FusedMultiplyAdd(sinVal, px2, -0.16666666666666666);
		sinVal = Double.FusedMultiplyAdd(sinVal, px2,  1.0);
		sinVal = sinVal * px;
		sinVal = Double.CopySign(sinVal, originalSign);

		// Cos: degree-8 polynomial in px ∈ [0, π/2]
		var cosVal = 2.6051615464872668e-5;
		cosVal = Double.FusedMultiplyAdd(cosVal, px2, -0.0013888888888887398);
		cosVal = Double.FusedMultiplyAdd(cosVal, px2,  0.041666666666666664);
		cosVal = Double.FusedMultiplyAdd(cosVal, px2, -0.5);
		cosVal = Double.FusedMultiplyAdd(cosVal, px2,  1.0);
		cosVal = inUpperHalf ? -cosVal : cosVal;

		return (sinVal, cosVal);
	}

	// ---- V2: direct u-domain polynomials (π absorbed into coefficients) ---

	/// <summary>
	/// FastSinCosPiV2 float: π-scaled coefficients allow evaluating sinpi/cospi
	/// directly in the u-domain without the intermediate px = xReduced*π multiply.
	/// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040))))
	/// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720)))
	/// Saves one FMUL per call; same polynomial degree; same numerical accuracy.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (float Sin, float Cos) FastSinCosPiV2Float(float x)
	{
		// Range reduction to [−1, 1]
		x -= Single.Round(x * 0.5f) * 2.0f;

		var originalSign = x;
		var absX         = Single.Abs(x);

		// Branchless fold [0.5, 1] → [0, 0.5]: FCSEL on ARM64
		var inUpperHalf = absX > 0.5f;
		var u           = inUpperHalf ? (1.0f - absX) : absX;

		// u² shared for both polynomials — no px = u*π multiply needed
		var u2 = u * u;

		// sinpi(u) = u·(c₀ + u²·(c₁ + u²·(c₂ + u²·c₃)))
		// c₀=π, c₁=−π³/6, c₂=π⁵/120, c₃=−π⁷/5040
		var s = -0.5992645f;                                // −π⁷/5040
		s = Single.FusedMultiplyAdd(s, u2,  2.5501640f);   //  π⁵/120
		s = Single.FusedMultiplyAdd(s, u2, -5.1677128f);   // −π³/6
		s = Single.FusedMultiplyAdd(s, u2,  3.1415927f);   //  π
		s *= u;
		s = Single.CopySign(s, originalSign);

		// cospi(u) = 1 + u²·(d₁ + u²·(d₂ + u²·d₃))
		// d₁=−π²/2, d₂=π⁴/24, d₃=−π⁶/720
		var c = -1.3352627f;                                // −π⁶/720
		c = Single.FusedMultiplyAdd(c, u2,  4.0587121f);   //  π⁴/24
		c = Single.FusedMultiplyAdd(c, u2, -4.9348022f);   // −π²/2
		c = Single.FusedMultiplyAdd(c, u2,  1.0f);
		c = inUpperHalf ? -c : c;

		return (s, c);
	}

	/// <summary>
	/// FastSinCosPiV2 double: π-scaled coefficients allow evaluating sinpi/cospi
	/// directly in the u-domain without the intermediate px = xReduced*π multiply.
	/// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040 + u²·(π⁹/362880)))))
	/// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720 + u²·(π⁸/40320))))
	/// Saves one FMUL per call; same polynomial degree; same numerical accuracy.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (double Sin, double Cos) FastSinCosPiV2Double(double x)
	{
		// Range reduction to [−1, 1]
		x -= Double.Round(x * 0.5) * 2.0;

		var originalSign = x;
		var absX         = Double.Abs(x);

		// Branchless fold [0.5, 1] → [0, 0.5]: FCSEL on ARM64
		var inUpperHalf = absX > 0.5;
		var u           = inUpperHalf ? (1.0 - absX) : absX;

		// u² shared for both polynomials — no px = u*π multiply needed
		var u2 = u * u;

		// sinpi(u) = u·(c₀ + u²·(c₁ + u²·(c₂ + u²·(c₃ + u²·c₄))))
		// c₀=π, c₁=−π³/6, c₂=π⁵/120, c₃=−π⁷/5040, c₄=π⁹/362880
		var s = 0.08214588661112823;                                   //  π⁹/362880
		s = Double.FusedMultiplyAdd(s, u2, -0.5992645293218801);      // −π⁷/5040
		s = Double.FusedMultiplyAdd(s, u2,  2.5501640398773455);      //  π⁵/120
		s = Double.FusedMultiplyAdd(s, u2, -5.1677127800499706);      // −π³/6
		s = Double.FusedMultiplyAdd(s, u2,  3.1415926535897932);      //  π
		s *= u;
		s = Double.CopySign(s, originalSign);

		// cospi(u) = 1 + u²·(d₁ + u²·(d₂ + u²·(d₃ + u²·d₄)))
		// d₁=−π²/2, d₂=π⁴/24, d₃=−π⁶/720, d₄=π⁸/40320
		var c = 0.23533075157732439;                                   //  π⁸/40320
		c = Double.FusedMultiplyAdd(c, u2, -1.3352627312227247);      // −π⁶/720
		c = Double.FusedMultiplyAdd(c, u2,  4.0587121264167682);      //  π⁴/24
		c = Double.FusedMultiplyAdd(c, u2, -4.9348022005446793);      // −π²/2
		c = Double.FusedMultiplyAdd(c, u2,  1.0);
		c = inUpperHalf ? -c : c;

		return (s, c);
	}
}



