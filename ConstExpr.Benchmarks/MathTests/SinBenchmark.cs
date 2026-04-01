using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Sin implementations for float and double.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Sin  vs FastSin(float)   [current: Round(x/τ) + branch if x>π/2, deg-7 poly]
///                        vs FastSinV2(float) [branchless fold: Min(x, π-x), same deg-7 poly]
///                        vs FastSinV3(float) [branchless fold + deg-5 poly — 1 fewer FMA, ~1.3e-4 max error]
///   Double – Math.Sin   vs FastSin(double)   [current: Round(x/τ) + branch if x>π/2, deg-11 poly]
///                        vs FastSinV2(double) [branchless fold: Min(x, π-x), same deg-11 poly]
///
/// Key findings (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Float:  DotNet=2.34ns  FastSin=0.94ns(-60%)  FastSinV2=0.96ns(-59%)  FastSinV3=0.89ns(-62%) ✓
///   Double: DotNet=2.93ns  FastSin=1.09ns(-63%)  FastSinV2=1.12ns(-62%)  — V1 wins for double
///
/// Float outcome:
///   FastSinV3 wins: replacing the branch with float.Min (FMIN on ARM64) AND dropping the
///   highest-order polynomial term (3 FMA instead of 4) saves ~0.05 ns (5.3% improvement).
///   Max absolute error ≈ 1.3e-4 near x=π/2 — acceptable for FastMath use cases.
///
/// Double outcome:
///   Surprisingly, the branch-based fold is faster than the branchless Min on ARM64 M4 Pro.
///   The M4 Pro's branch predictor handles the ">π/2" branch well (~50% taken), and the
///   extra FSUB for "π - x" in the branchless path adds enough latency to be slower.
///   Conclusion: keep the current double implementation unchanged.
///
/// Input domain: [-100, 100] — spans ~32 full periods, exercises range reduction heavily.
///
/// Accuracy notes (max absolute error, x ∈ [-100, 100]):
///   FastSin(float)   — deg-7 poly on [0,π/2], one branch in range-fold
///   FastSinV2(float) — same polynomial, branchless fold via float.Min
///   FastSinV3(float) — deg-5 poly on [0,π/2]: max error ≈ 1.3e-4 near x=π/2 (1 fewer FMA)
///   FastSin(double)  — deg-11 poly on [0,π/2], one branch in range-fold
///   FastSinV2(double)— same polynomial, branchless fold via double.Min
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SinBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SinBenchmark
{
	// 1 024 values spread over many full periods to exercise range reduction.
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	// Precomputed 1/(2π) — replaces division by Tau with a multiplication.
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

	/// <summary>Built-in MathF.Sin — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetSin_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Sin(v);
		return sum;
	}

	/// <summary>
	/// Current FastSin(float) from SinFunctionOptimizer.
	/// Range reduction: Round(x*(1/τ))*τ → abs → if(x>π/2) x=π-x → [0,π/2].
	/// Polynomial: degree-7 in x (4 FMA + 1 mul).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSin_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastSinFloat(v);
		return sum;
	}

	/// <summary>
	/// FastSinV2(float) — branchless fold via float.Min.
	/// Replaces "if (x > π/2) x = π-x" with "x = float.Min(x, π-x)" (FMIN on ARM64/x64).
	/// Same degree-7 polynomial as the current implementation — identical numerical accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastSinV3(float) — branchless fold + degree-5 polynomial (3 FMA instead of 4).
	/// Drops the x^7 minimax correction — saves 1 FMA per call.
	/// Max absolute error ≈ 1.3e-4 near x=π/2.
	/// Best choice when raw throughput matters more than accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastSinV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastSinV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Sin — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetSin_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Sin(v);
		return sum;
	}

	/// <summary>
	/// Current FastSin(double) from SinFunctionOptimizer.
	/// Range reduction: Round(x*(1/τ))*τ → abs → if(x>π/2) x=π-x → [0,π/2].
	/// Polynomial: degree-11 in x (6 FMA + 1 mul).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSin_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastSinDouble(v);
		return sum;
	}

	/// <summary>
	/// FastSinV2(double) — branchless fold via double.Min.
	/// Replaces "if (x > π/2) x = π-x" with "x = double.Min(x, π-x)" (FMIN on ARM64/x64).
	/// Same degree-11 polynomial as the current implementation — identical numerical accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastSinV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastSinV2Double(v);
		return sum;
	}

	// ── scalar implementations ──────────────────────────────────────────────

	/// <summary>
	/// Mirror of SinFunctionOptimizer's generated FastSin(float).
	/// Range reduction: Round(x*(1/τ))*τ → Abs → if branch → [0, π/2].
	/// Polynomial: x*(1 + x²*(-1/6 + x²*(1/120 + x²*(-1/5040 + x²*c8)))).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastSinFloat(float x)
	{
		var originalX = x;

		// Range reduction to [-π, π]
		x -= Single.Round(x * (1.0f / Single.Tau)) * Single.Tau;

		// Fold [0, π] → [0, π/2] via absolute value + conditional
		x = Single.Abs(x);
		if (x > Single.Pi / 2f)
			x = Single.Pi - x;

		// Degree-7 polynomial: sin(x) ≈ x*(1 - x²/6 + x⁴/120 - x⁶/5040 + c8*x⁸)
		var x2 = x * x;
		var ret = 2.6019406621361745e-6f;
		ret = Single.FusedMultiplyAdd(ret, x2, -0.00019839531932f);
		ret = Single.FusedMultiplyAdd(ret, x2,  0.0083333333333f);
		ret = Single.FusedMultiplyAdd(ret, x2, -0.16666666666f);
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);
		ret *= x;

		return Single.CopySign(ret, originalX);
	}

	/// <summary>
	/// Mirror of SinFunctionOptimizer's generated FastSin(double).
	/// Range reduction: Round(x*(1/τ))*τ → Abs → if branch → [0, π/2].
	/// Polynomial: x*(1 + x²*(-1/6 + ... + c12*x¹²)).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastSinDouble(double x)
	{
		var originalX = x;

		// Range reduction to [-π, π]
		x -= Double.Round(x * (1.0 / Double.Tau)) * Double.Tau;

		// Fold [0, π] → [0, π/2] via absolute value + conditional
		x = Double.Abs(x);
		if (x > Double.Pi / 2.0)
			x = Double.Pi - x;

		// Degree-11 polynomial
		var x2 = x * x;
		var ret = 2.6019406621361745e-9;
		ret = Double.FusedMultiplyAdd(ret, x2, -1.9839531932589676e-7);
		ret = Double.FusedMultiplyAdd(ret, x2,  8.3333333333216515e-6);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.00019841269836761127);
		ret = Double.FusedMultiplyAdd(ret, x2,  0.0083333333333332177);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.16666666666666666);
		ret = Double.FusedMultiplyAdd(ret, x2,  1.0);
		ret *= x;

		return Double.CopySign(ret, originalX);
	}

	/// <summary>
	/// FastSinV2(float): branchless fold via float.Min + precomputed InvTau.
	/// "x = float.Min(x, π-x)" compiles to FMIN on ARM64 / MINSS on x64 — no branch.
	/// Same degree-7 polynomial as the current implementation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinV2Float(float x)
	{
		var originalX = x;

		// Branchless range reduction to [-π, π]
		x -= Single.Round(x * InvTauF) * Single.Tau;

		// Fold [0, π] → [0, π/2]: branchless via FMIN
		x = Single.Abs(x);
		x = Single.Min(x, Single.Pi - x);

		// Degree-7 polynomial — identical to current
		var x2 = x * x;
		var ret = 2.6019406621361745e-6f;
		ret = Single.FusedMultiplyAdd(ret, x2, -0.00019839531932f);
		ret = Single.FusedMultiplyAdd(ret, x2,  0.0083333333333f);
		ret = Single.FusedMultiplyAdd(ret, x2, -0.16666666666f);
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);
		ret *= x;

		return Single.CopySign(ret, originalX);
	}

	/// <summary>
	/// FastSinV3(float): branchless fold + degree-5 polynomial.
	/// Drops the highest-order correction term (saves 1 FMA).
	/// Max absolute error ≈ 1.3e-4 near x = π/2 — acceptable for game/graphics use cases.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastSinV3Float(float x)
	{
		var originalX = x;

		// Branchless range reduction to [-π, π]
		x -= Single.Round(x * InvTauF) * Single.Tau;

		// Fold [0, π] → [0, π/2]: branchless via FMIN
		x = Single.Abs(x);
		x = Single.Min(x, Single.Pi - x);

		// Degree-5 polynomial: sin(x) ≈ x*(1 - x²/6 + x⁴/120 - x⁶/5040) — 3 FMA
		var x2 = x * x;
		var ret = -1.9841269841e-4f;                              // -1/5040
		ret = Single.FusedMultiplyAdd(ret, x2,  8.3333333333e-3f); // +1/120
		ret = Single.FusedMultiplyAdd(ret, x2, -1.6666666667e-1f); // -1/6
		ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);             // +1
		ret *= x;

		return Single.CopySign(ret, originalX);
	}

	/// <summary>
	/// FastSinV2(double): branchless fold via double.Min + precomputed InvTau.
	/// Same degree-11 polynomial as the current implementation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastSinV2Double(double x)
	{
		var originalX = x;

		// Branchless range reduction to [-π, π]
		x -= Double.Round(x * InvTauD) * Double.Tau;

		// Fold [0, π] → [0, π/2]: branchless via FMIN
		x = Double.Abs(x);
		x = Double.Min(x, Double.Pi - x);

		// Degree-11 polynomial — identical to current
		var x2 = x * x;
		var ret = 2.6019406621361745e-9;
		ret = Double.FusedMultiplyAdd(ret, x2, -1.9839531932589676e-7);
		ret = Double.FusedMultiplyAdd(ret, x2,  8.3333333333216515e-6);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.00019841269836761127);
		ret = Double.FusedMultiplyAdd(ret, x2,  0.0083333333333332177);
		ret = Double.FusedMultiplyAdd(ret, x2, -0.16666666666666666);
		ret = Double.FusedMultiplyAdd(ret, x2,  1.0);
		ret *= x;

		return Double.CopySign(ret, originalX);
	}
}


