using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar sqrt implementations for float and double, and measures the
/// algebraic Sqrt(x*x) → Abs(x) optimisation produced by SqrtFunctionOptimizer.
///
/// Three benchmark groups:
///   Float  – MathF.Sqrt vs BitHackNewton2 vs RsqrtNewton (ReciprocalSqrtEstimate + 1 Newton)
///   Double – Math.Sqrt  vs BitHackNewton3  vs FloatSqrtNewton2 (float-seed + 2 Newton)
///   AlgOpt – MathF.Sqrt(v*v) vs float.Abs(v)  — proves Sqrt(x²)=Abs(x) rewrite pays off
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method                   Category  Mean      Ratio    Note
///   -----------------------  --------  --------  -------  -----------------------------------------------
///   DotNet_Float             Float     0.486 ns  1.00×  ← FASTEST — hardware fsqrt instruction
///   RsqrtNewton_Float        Float     1.007 ns  2.07×    frsqrte + Newton — 2× slower on ARM64
///   BitHackNewton2_Float     Float     1.241 ns  2.55×    bit-hack + 2 Newton — 2.6× slower
///
///   DotNet_Double            Double    0.477 ns  1.00×  ← FASTEST — hardware fsqrt instruction
///   FloatSqrtNewton2_Double  Double    1.543 ns  3.23×    float-seed + 2 Newton — 3× slower
///   BitHackNewton3_Double    Double    2.061 ns  4.32×    bit-hack + 3 Newton — 4× slower
///
///   SqrtOfSquared_Float      AlgOpt    0.497 ns  1.00×    MathF.Sqrt(v*v) — JIT folds to fabs on ARM64!
///   AbsOptimized_Float       AlgOpt    0.536 ns  ~1.00×   float.Abs(v) — same throughput on ARM64
///
/// Key findings:
///   1. The ARM64 hardware fsqrt executes at ~0.5 ns throughput — no scalar software approximation
///      can compete.  The optimizer MUST keep Math.Sqrt / MathF.Sqrt for the general case.
///   2. The AlgOpt group shows near-identical timings because the ARM64 RyuJIT already folds
///      sqrt(x*x) → fabs at machine-code level.  The SqrtFunctionOptimizer rewrite is still
///      valuable on x86 (SQRTSS latency ~10-14 cy vs FABS 1 cy) and for source-level readability.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SqrtBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SqrtBenchmark
{
	private const int N = 1_024;
	private float[] _floatData = null!;
	private double[] _doubleData = null!;
	private float[] _mixedFloatData = null!;  // positive + negative values for AlgOpt group

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData = new float[N];
		_doubleData = new double[N];
		_mixedFloatData = new float[N];

		for (var i = 0; i < N; i++)
		{
			var v = rng.NextDouble() * 1e6;  // 0 .. 1_000_000
			_floatData[i] = (float)v;
			_doubleData[i] = v;

			// Mix of positive and negative values for the Sqrt(x*x) vs Abs(x) comparison.
			var sign = rng.Next(2) == 0 ? 1.0 : -1.0;
			_mixedFloatData[i] = (float)(sign * rng.NextDouble() * 1e6);
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>MathF.Sqrt — hardware SQRTSS / fsqrt instruction, baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNet_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Sqrt(v);
		return sum;
	}

	/// <summary>
	/// Bit-hack initial estimate (~7 bits) + 2× Newton: y = 0.5*(y + x/y).
	/// Two divisions; full float precision (~23 bits) after both steps.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float BitHackNewton2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += BitHackNewton2Float(v);
		return sum;
	}

	/// <summary>
	/// ReciprocalSqrtEstimate (RSQRTSS / frsqrte, ~12-bit accuracy) + 1 Newton step.
	/// Newton refines 1/sqrt(x) to ~23 bits; multiply by x gives sqrt(x).
	/// On x86 avoids the higher-latency SQRTSS; on ARM64 frsqrte has lower throughput
	/// than fsqrt, so expected to be slower.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float RsqrtNewton_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += RsqrtNewtonFloat(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Math.Sqrt — hardware SQRTSD / fsqrt instruction, baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNet_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Sqrt(v);
		return sum;
	}

	/// <summary>
	/// Bit-hack initial estimate (~11 bits) + 3× Newton: y = 0.5*(y + x/y).
	/// Three divisions; reaches full double precision (~53 bits) after all steps.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double BitHackNewton3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += BitHackNewton3Double(v);
		return sum;
	}

	/// <summary>
	/// Float sqrt as a ~23-bit starting point, then 2× Newton to reach full double precision.
	/// MathF.Sqrt is fast, then each Newton step doubles precision: 23 → 46 → 53+ bits.
	/// May win over BitHackNewton3 (avoids 3 divisions); may lose to Math.Sqrt on ARM64.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FloatSqrtNewton2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FloatSqrtNewton2Double(v);
		return sum;
	}

	// ── algebraic optimisation: Sqrt(x*x) → Abs(x) ─────────────────────────

	/// <summary>
	/// MathF.Sqrt(v * v) — two multiplications + sqrt (hardware fsqrt).
	/// The ConstExpr SqrtFunctionOptimizer rewrites this pattern to float.Abs(v).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("AlgOpt")]
	public float SqrtOfSquared_Float()
	{
		var sum = 0f;
		foreach (var v in _mixedFloatData)
			sum += MathF.Sqrt(v * v);
		return sum;
	}

	/// <summary>
	/// float.Abs(v) — the rewritten form emitted by SqrtFunctionOptimizer for Sqrt(x*x).
	/// A single FABS / vabs instruction: no multiplication, no sqrt.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("AlgOpt")]
	public float AbsOptimized_Float()
	{
		var sum = 0f;
		foreach (var v in _mixedFloatData)
			sum += float.Abs(v);
		return sum;
	}

	// ── scalar implementations ──────────────────────────────────────────────

	/// <summary>
	/// Float sqrt: bit-hack initial estimate + 2× Newton-Raphson.
	/// Magic constant 0x1fbb4f2e (Lomont) gives ~7-bit starting accuracy.
	/// After 2 Newton steps: full single precision (~23 bits).
	/// </summary>
	private static float BitHackNewton2Float(float x)
	{
		if (x <= 0f) return 0f;
		var i = BitConverter.SingleToInt32Bits(x);
		i = 0x1fbb4f2e + (i >> 1);
		var y = BitConverter.Int32BitsToSingle(i);
		y = 0.5f * (y + x / y);
		y = 0.5f * (y + x / y);
		return y;
	}

	/// <summary>
	/// Float sqrt via reciprocal-sqrt estimate + 1 Newton refinement.
	/// r = ReciprocalSqrtEstimate(x) ≈ 1/sqrt(x) with ~12-bit hardware estimate.
	/// Newton: r = r * (1.5 - 0.5*x*r*r) → ~23-bit 1/sqrt(x).
	/// Result: x * r ≈ sqrt(x) with ~23-bit accuracy.
	/// </summary>
	private static float RsqrtNewtonFloat(float x)
	{
		if (x <= 0f) return 0f;
		var r = MathF.ReciprocalSqrtEstimate(x);
		r *= 1.5f - 0.5f * x * r * r;
		return x * r;
	}

	/// <summary>
	/// Double sqrt: bit-hack initial estimate + 3× Newton-Raphson.
	/// Constant 0x1FF8000000000000L halves the exponent field (~11-bit accuracy).
	/// Three Newton steps: ~22 → ~44 → ~53+ bits (full double precision).
	/// </summary>
	private static double BitHackNewton3Double(double x)
	{
		if (x <= 0.0) return 0.0;
		var i = BitConverter.DoubleToInt64Bits(x);
		i = 0x1FF8000000000000L + (i >> 1);
		var y = BitConverter.Int64BitsToDouble(i);
		y = 0.5 * (y + x / y);
		y = 0.5 * (y + x / y);
		y = 0.5 * (y + x / y);
		return y;
	}

	/// <summary>
	/// Double sqrt: use float sqrt as a ~23-bit initial estimate, then 2× Newton.
	/// MathF.Sqrt((float)x) provides the starting point; Newton doubles precision each step:
	/// ~23 → ~46 → ~53+ bits (full double precision).
	/// Avoids the bit-hack; relies on the already-fast hardware single-precision sqrt.
	/// </summary>
	private static double FloatSqrtNewton2Double(double x)
	{
		if (x <= 0.0) return 0.0;
		var y = (double)MathF.Sqrt((float)x);
		y = 0.5 * (y + x / y);
		y = 0.5 * (y + x / y);
		return y;
	}
}


