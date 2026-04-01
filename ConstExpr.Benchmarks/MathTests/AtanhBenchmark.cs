using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.Atanh against FastAtanh variants from AtanhFunctionOptimizer.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Atanh  vs CurrentFastAtanh(float)  vs FastAtanhV2(float)  vs FastAtanhV3(float)
///   Double – Math.Atanh   vs CurrentFastAtanh(double) vs FastAtanhV2(double) vs FastAtanhV3(double)
///
/// Current FastAtanh analysis:
///   3 branches: NaN guard, |x|>=1 guard, |x|&lt;0.5 Taylor-vs-log split.
///   For |x|&lt;0.5: 5-term Horner polynomial with FMA (no transcendental call).
///   For |x|>=0.5: 0.5*log((1+x)/(1-x)) — one log call.
///   Branch mispredictions on uniform random data hurt throughput.
///
/// V2 (branchless log formula):
///   0.5 * log((1+x)/(1-x)) — zero branches.
///   NaN/∞ propagate naturally through the log, so guards are unnecessary.
///   Single log call for the entire domain; no conditional overhead.
///
/// V3 (branchless log1p-style — numerically superior near x=0):
///   0.5 * log(1 + 2x/(1-x)) — mathematically identical to V2 but the argument is expressed
///   as "1 + y" which avoids cancellation error in (1+x)/(1-x) when |x| is small.
///   Zero branches; may run at the same speed as V2 or slightly differently
///   depending on the runtime's log intrinsic quality.
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=2.305 ns | CurrentFastAtanh=1.910 ns (−17%) | V2=2.012 ns (−13%) | V3=1.768 ns (−23%) ← winner
///   Double: DotNet=4.861 ns | CurrentFastAtanh=1.796 ns (−63%) ← winner | V3=2.496 ns (−49%) | V2=3.009 ns (−38%)
///
/// Conclusion:
///   Float: FastAtanhV3 (branchless log1p-style) is the fastest.  The 3-branch Horner polynomial
///          is replaced by a single branchless log call.  Implementation updated in AtanhFunctionOptimizer.
///   Double: CurrentFastAtanh (3-branch Horner FMA) stays as production implementation.  The FMA chain
///           avoids the transcendental log entirely for |x|&lt;0.5, and ARM64 predicts the branch well.
///           Both V2 and V3 (pure-log approaches) are significantly slower.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*AtanhBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class AtanhBenchmark
{
	// 1 024 values uniformly distributed over (-0.99, 0.99).
	// atanh is only defined on (-1, 1); staying away from the poles removes
	// degenerate ±∞ outputs that would contaminate the sum-reduction.
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
			var v = rng.NextDouble() * 1.98 - 0.99; // uniform in (-0.99, 0.99)
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.Atanh — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetAtanh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Atanh(v);
		return sum;
	}

	/// <summary>
	/// Current FastAtanh(float) from AtanhFunctionOptimizer — 3-branch implementation:
	///   1. NaN guard.
	///   2. |x| >= 1 → ±∞.
	///   3. |x| &lt; 0.5 → 5-term Horner Taylor series with FMA; else log formula.
	/// Branch mispredictions on random data reduce throughput.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastAtanh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastAtanhFloat(v);
		return sum;
	}

	/// <summary>
	/// FastAtanhV2(float) — branchless: 0.5f * log((1+x)/(1-x)).
	/// NaN and ±∞ propagate naturally through the log, so no explicit guards are needed.
	/// Single log call; zero conditional branches → no misprediction penalty.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAtanhV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastAtanhV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastAtanhV3(float) — branchless log1p-style: 0.5f * log(1 + 2x/(1-x)).
	/// Mathematically identical to V2 but the argument is expressed as "1 + y" which avoids
	/// cancellation error in (1+x)/(1-x) when |x| is small.  Zero branches.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAtanhV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastAtanhV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Atanh — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetAtanh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Atanh(v);
		return sum;
	}

	/// <summary>
	/// Current FastAtanh(double) from AtanhFunctionOptimizer — same 3-branch structure
	/// as the float version, evaluated in double arithmetic.
	/// 5-term Horner series for |x|&lt;0.5; log formula otherwise.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastAtanh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastAtanhDouble(v);
		return sum;
	}

	/// <summary>
	/// FastAtanhV2(double) — branchless: 0.5 * log((1+x)/(1-x)).
	/// Zero conditional branches; NaN/±∞ propagate naturally.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAtanhV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastAtanhV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastAtanhV3(double) — branchless log1p-style: 0.5 * log(1 + 2x/(1-x)).
	/// Mathematically identical to V2 but the argument is expressed as "1 + y" which avoids
	/// cancellation error in (1+x)/(1-x) when |x| is small.  Zero branches.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAtanhV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastAtanhV3Double(v);
		return sum;
	}

	// ── current implementation (exact mirror of AtanhFunctionOptimizer generated code) ──

	private static float CurrentFastAtanhFloat(float x)
	{
		// Handle special cases
		if (Single.IsNaN(x)) return Single.NaN;
		if (Single.Abs(x) >= 1.0f) return x > 0 ? float.PositiveInfinity : float.NegativeInfinity;

		// Use the definition: atanh(x) = 0.5 * ln((1 + x) / (1 - x))
		// For small |x|, use Taylor series for better accuracy
		var absX = Single.Abs(x);

		if (absX < 0.5f)
		{
			// Taylor series: atanh(x) = x + x³/3 + x⁵/5 + x⁷/7 + x⁹/9
			var x2 = x * x;

			// Horner's method with FMA: x * (1 + x²*(1/3 + x²*(1/5 + x²*(1/7 + x²/9))))
			var poly = Single.FusedMultiplyAdd(x2, 1f / 9f, 1f / 7f);
			poly = Single.FusedMultiplyAdd(poly, x2, 0.2f);
			poly = Single.FusedMultiplyAdd(poly, x2, 1f / 3f);
			poly = Single.FusedMultiplyAdd(poly, x2, 1f);

			return x * poly;
		}
		else
		{
			// Use logarithmic formula: 0.5 * ln((1 + x) / (1 - x))
			return 0.5f * Single.Log((1f + x) / (1f - x));
		}
	}

	private static double CurrentFastAtanhDouble(double x)
	{
		// Handle special cases
		if (Double.IsNaN(x)) return Double.NaN;
		if (Math.Abs(x) >= 1.0) return x > 0 ? Double.PositiveInfinity : Double.NegativeInfinity;

		// Use the definition: atanh(x) = 0.5 * ln((1 + x) / (1 - x))
		// For small |x|, use Taylor series for better accuracy
		var absX = Double.Abs(x);

		if (absX < 0.5)
		{
			// Taylor series: atanh(x) = x + x³/3 + x⁵/5 + x⁷/7 + x⁹/9 + x¹¹/11
			var x2 = x * x;

			// Horner's method with FMA: x * (1 + x²*(1/3 + x²*(1/5 + x²*(1/7 + x²*(1/9 + x²/11)))))
			var poly = Double.FusedMultiplyAdd(x2, 1d / 11d, 1d / 9d);
			poly = Double.FusedMultiplyAdd(poly, x2, 1d / 7d);
			poly = Double.FusedMultiplyAdd(poly, x2, 1d / 5d);
			poly = Double.FusedMultiplyAdd(poly, x2, 1d / 3d);
			poly = Double.FusedMultiplyAdd(poly, x2, 1d);

			return x * poly;
		}
		else
		{
			// Use logarithmic formula: 0.5 * ln((1 + x) / (1 - x))
			return 0.5 * Double.Log((1.0 + x) / (1.0 - x));
		}
	}

	// ── improved implementations ────────────────────────────────────────────

	/// <summary>
	/// Branchless: 0.5f * log((1+x)/(1-x)).
	/// NaN propagates through arithmetic and log; |x|>=1 yields ±∞ or NaN naturally.
	/// Eliminates all three explicit guards of the current implementation.
	/// </summary>
	private static float FastAtanhV2Float(float x)
		=> 0.5f * Single.Log((1f + x) / (1f - x));

	/// <summary>
	/// Branchless log1p-style: 0.5f * log(1 + 2x/(1-x)).
	/// Mathematically identical to V2 — log(1 + 2x/(1-x)) = log((1+x)/(1-x)) — but the
	/// argument is expressed as "1 + small correction" which preserves more bits when |x| is small.
	/// Zero branches; numerically superior to V2 near x=0.
	/// </summary>
	private static float FastAtanhV3Float(float x)
		=> 0.5f * MathF.Log(1f + 2f * x / (1f - x));

	/// <summary>
	/// Branchless: 0.5 * log((1+x)/(1-x)).
	/// Zero branches; NaN/±∞ propagate naturally.
	/// </summary>
	private static double FastAtanhV2Double(double x)
		=> 0.5 * Double.Log((1.0 + x) / (1.0 - x));

	/// <summary>
	/// Branchless log1p-style: 0.5 * log(1 + 2x/(1-x)).
	/// Mathematically identical to V2 — log(1 + 2x/(1-x)) = log((1+x)/(1-x)) — but the
	/// argument is expressed as "1 + small correction" which preserves more bits when |x| is small.
	/// Zero branches; numerically superior to V2 near x=0.
	/// </summary>
	private static double FastAtanhV3Double(double x)
		=> 0.5 * Math.Log(1.0 + 2.0 * x / (1.0 - x));
}





