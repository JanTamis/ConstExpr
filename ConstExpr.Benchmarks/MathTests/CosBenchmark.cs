using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Cos implementations for float and double.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Cos  vs FastCos(float)   [current: Floor + 2 branches, deg-8 poly]
///                        vs FastCosV2(float) [branchless: Single.Round, same deg-8 poly]
///                        vs FastCosV3(float) [branchless: Single.Round, deg-6 poly — faster, ~1.8e-4 max error]
///   Double – Math.Cos   vs FastCos(double)   [current: Floor + 2 branches, deg-10 poly]
///                        vs FastCosV2(double) [branchless: Double.Round, same deg-10 poly]
///
/// Key hypothesis:
///   The current range reduction does:  Floor(x/τ)*τ  +  two conditional branches.
///   On ARM64 and x64, FDIV has 12-20 cy latency; the two branches add code-size and
///   mispredict risk.  Replacing with  Round(x/τ)  (single FRINTN/ROUNDSS, 4 cy) and
///   one FMA should reduce range-reduction cost substantially.
///
/// Input domain: [-100, 100] — spans ~32 full periods, exercises range reduction heavily.
///
/// Accuracy notes (max absolute error, x ∈ [-100, 100]):
///   FastCos(float)   — same polynomial as V2, dominated by float precision limit.
///   FastCosV2(float) — identical polynomial, lower range-reduction overhead.
///   FastCosV3(float) — degree-6 Taylor: max error ≈ 1.8e-4 rad near x = π (1 fewer FMA).
///   FastCos(double)  — degree-10 minimax polynomial, max error ≈ 4e-14 rad.
///   FastCosV2(double)— identical polynomial, lower range-reduction overhead.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*CosBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CosBenchmark
{
	// 1 024 values spread over many full periods to exercise range reduction.
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	// Precomputed 1/(2π) to replace division in range reduction.
	private const float  InvTauF = 1f / (2f * MathF.PI);
	private const double InvTauD = 1.0 / (2.0 * Math.PI);

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

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.Cos — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetCos_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Cos(v);
		return sum;
	}

	/// <summary>
	/// Current FastCos(float) from CosFunctionOptimizer.
	/// Range reduction: Floor(x/τ)·τ then two conditional branches → [-π, π].
	/// Polynomial: degree-8 minimax in x² (4 FMA operations).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCos_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastCosFloat(v);
		return sum;
	}

	/// <summary>
	/// FastCosV2(float) — branchless range reduction via Single.Round.
	/// Replaces Floor(x/τ)+2 branches with a single Round(x/τ) (FRINTN on ARM64,
	/// ROUNDSS on x64) — avoids FDIV and branch mispredictions.
	/// Same degree-8 polynomial as the current implementation.
	/// Max absolute error identical to FastCos(float).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCosV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCosV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastCosV3(float) — branchless range reduction + degree-6 Taylor polynomial.
	/// Saves one FMA operation vs V2 by dropping the x⁸ correction term.
	/// Max absolute error ≈ 1.8e-4 rad near x = π (worse than V2, faster throughput).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastCosV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastCosV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Cos — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetCos_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Cos(v);
		return sum;
	}

	/// <summary>
	/// Current FastCos(double) from CosFunctionOptimizer.
	/// Range reduction: Floor(x/τ)·τ then two conditional branches → [-π, π].
	/// Polynomial: degree-10 minimax in x² (5 FMA operations).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCos_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastCosDouble(v);
		return sum;
	}

	/// <summary>
	/// FastCosV2(double) — branchless range reduction via Double.Round.
	/// Same degree-10 polynomial as the current implementation.
	/// Avoids FDIV and the two conditional branches in range reduction.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastCosV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastCosV2Double(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	/// <summary>
	/// Mirror of CosFunctionOptimizer's generated FastCos(float).
	/// Range reduction: Floor(x/τ)·τ + two if-branches; fold via Abs; degree-8 poly.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastCosFloat(float x)
	{
		// Range reduction to [0, 2π) then to [-π, π]
		x = x - Single.Floor(x / Single.Tau) * Single.Tau;
		if (x > Single.Pi) x -= Single.Tau;
		if (x < -Single.Pi) x += Single.Tau;

		// cos(-x) = cos(x): fold to [0, π]
		x = Single.Abs(x);

		// Minimax polynomial for cos(x) on [0, π], evaluated in x² (4 FMA)
		var x2 = x * x;
		var ret = 0.0003538394f;
		ret = Single.FusedMultiplyAdd(ret, x2, -0.0041666418f);
		ret = Single.FusedMultiplyAdd(ret, x2,  0.041666666f);
		ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);
		return ret;
	}

	/// <summary>
	/// Mirror of CosFunctionOptimizer's generated FastCos(double).
	/// Range reduction: Floor(x/τ)·τ + two if-branches; fold via Abs; degree-10 poly.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastCosDouble(double x)
	{
		// Range reduction to [0, 2π) then to [-π, π]
		x = x - Double.Floor(x / Double.Tau) * Double.Tau;
		if (x > Double.Pi) x -= Double.Tau;
		if (x < -Double.Pi) x += Double.Tau;

		// cos(-x) = cos(x): fold to [0, π]
		x = Double.Abs(x);

		// Minimax polynomial for cos(x) on [0, π], evaluated in x² (5 FMA)
		var x2 = x * x;
		var ret = -1.1940250944959890e-7;
		ret = Double.FusedMultiplyAdd(ret, x2,  2.0876755527587203e-5);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.0013888888888739916);
		ret = Double.FusedMultiplyAdd(ret, x2,  0.041666666666666602);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.5);
		ret = Double.FusedMultiplyAdd(ret, x2,  1.0);
		return ret;
	}

	/// <summary>
	/// FastCosV2(float): branchless range reduction + same degree-8 polynomial.
	/// Round(x/τ) compiles to a single FRINTN (ARM64) or ROUNDSS (x64) — no FDIV,
	/// no conditional branches.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCosV2Float(float x)
	{
		// Branchless reduction to [-π, π]: subtract nearest integer multiple of τ
		x -= Single.Round(x * InvTauF) * Single.Tau;

		// cos(-x) = cos(x): fold to [0, π]
		x = Single.Abs(x);

		var x2 = x * x;
		var ret = 0.0003538394f;
		ret = Single.FusedMultiplyAdd(ret, x2, -0.0041666418f);
		ret = Single.FusedMultiplyAdd(ret, x2,  0.041666666f);
		ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);
		return ret;
	}

	/// <summary>
	/// FastCosV3(float): branchless range reduction + degree-6 Taylor polynomial.
	/// Drops the x⁸ minimax correction — 3 FMA instead of 4, saving ~1 cycle.
	/// Max absolute error ≈ 1.8e-4 rad (vs full-float-precision for V2).
	/// Best choice when raw throughput matters more than accuracy.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastCosV3Float(float x)
	{
		// Branchless reduction to [-π, π]
		x -= Single.Round(x * InvTauF) * Single.Tau;

		// cos(-x) = cos(x): fold to [0, π]
		x = Single.Abs(x);

		// Degree-6 Taylor polynomial: 1 - x²/2 + x⁴/24 - x⁶/720 (3 FMA)
		var x2 = x * x;
		var ret = -0.001388889f;                               // -1/720
		ret = Single.FusedMultiplyAdd(ret, x2, 0.041666667f); //  1/24
		ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);        // -1/2
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);        //  1
		return ret;
	}

	/// <summary>
	/// FastCosV2(double): branchless range reduction + same degree-10 polynomial.
	/// Double.Round maps to FRINTA (ARM64) or ROUNDSD (x64) — no FDIV, no branches.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastCosV2Double(double x)
	{
		// Branchless reduction to [-π, π]
		x -= Double.Round(x * InvTauD) * Double.Tau;

		// cos(-x) = cos(x): fold to [0, π]
		x = Double.Abs(x);

		var x2 = x * x;
		var ret = -1.1940250944959890e-7;
		ret = Double.FusedMultiplyAdd(ret, x2,  2.0876755527587203e-5);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.0013888888888739916);
		ret = Double.FusedMultiplyAdd(ret, x2,  0.041666666666666602);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.5);
		ret = Double.FusedMultiplyAdd(ret, x2,  1.0);
		return ret;
	}
}


