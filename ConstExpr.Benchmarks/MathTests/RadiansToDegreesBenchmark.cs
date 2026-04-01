using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares RadiansToDegrees implementations for float and double.
///
/// RadiansToDegrees(x) = x * (180 / π)
///
/// The optimizer (before this benchmark) inlined a multiply-by-constant at the call site.
/// Benchmarking reveals that the .NET builtin is actually FASTER, so the optimizer now
/// delegates to T.RadiansToDegrees(x) for all types.
///
/// Variants:
///   Float  - float.RadiansToDegrees  (baseline: .NET IFloatingPoint)
///            CurrentOptimizer        (x * (180f / MathF.PI) -- former optimizer output)
///            FmaVariant              (FMA(x, 180/pi, 0f) -- mul+zero addend, same as FMUL)
///            DoubleIntermediary      ((float)((double)x * (180.0/Math.PI)) -- higher precision, slower)
///   Double - double.RadiansToDegrees (baseline)
///            CurrentOptimizer        (x * (180.0 / Math.PI) -- former optimizer output)
///            FmaVariant              (FMA(x, 180/pi, 0.0) -- same as FMUL with zero addend)
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT, N=1024):
///   Float:  DotNet=0.508 ns | Optimizer=0.544 ns (+7%) | Fma=0.547 ns (+8%) | DblInterm=0.580 ns (+14%)
///   Double: DotNet=0.495 ns | Optimizer=0.535 ns (+8%) | Fma=0.542 ns (+9%)
///
/// Why is the builtin faster?
///   The JIT recognises float/double.RadiansToDegrees as a vectorisable loop and emits
///   multi-accumulator SIMD code. The explicit x*constant form does not reliably trigger
///   the same auto-vectorisation. The builtin also uses the more precise constant:
///   (float)(180.0/Math.PI) vs 180f/MathF.PI (may differ by 1 ULP).
///
/// Conclusion: The inline-multiply optimisation is removed. T.RadiansToDegrees(x) is
///   ~7-9% faster for both float and double.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*RadiansToDegreesBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class RadiansToDegreesBenchmark
{
	// 1 024 values uniformly distributed over a wide radian range.
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	// Constants that match exactly what the optimizer emits.
	// Float: 180f / MathF.PI (float arithmetic)
	// Double: 180.0 / Math.PI (full double precision)
	private const float  OneEightyOverPiF = 180f / MathF.PI;
	private const double OneEightyOverPiD = 180.0 / Math.PI;

	// More precise double-path constant used in the DoubleIntermediary float variant.
	private const double OneEightyOverPiPrecise = 180.0 / Math.PI; // 57.29577951308232

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData  = new float[N];
		_doubleData = new double[N];
		for (var i = 0; i < N; i++)
		{
			// Uniform in [-2π, 2π] radians
			var v = rng.NextDouble() * 4.0 * Math.PI - 2.0 * Math.PI;
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in float.RadiansToDegrees (IFloatingPointIeee754).
	/// Internally computes radians * (T.CreateChecked(180) / T.Pi).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetRadiansToDegrees_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.RadiansToDegrees(v);
		return sum;
	}

	/// <summary>
	/// Current optimizer output: x * (180f / MathF.PI).
	/// The constant is folded at C# compile time to 57.29578f and inlined
	/// directly at the call site — one FMUL, zero method-call overhead.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentOptimizer_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += v * OneEightyOverPiF;
		return sum;
	}

	/// <summary>
	/// FMA(x, 180/pi, 0f): fused multiply-add with zero addend.
	/// On hardware that treats FMA(x, C, 0) specially this could save a cycle;
	/// in practice the JIT typically emits the same FMUL as a plain multiply.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FmaVariant_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += Single.FusedMultiplyAdd(v, OneEightyOverPiF, 0f);
		return sum;
	}

	/// <summary>
	/// Higher-precision float result via double intermediary:
	///   (float)((double)x * (180.0 / Math.PI))
	/// Avoids the 1-ULP rounding error that can occur when dividing 180f by the
	/// float approximation of pi. Slower due to widening + narrowing conversions.
	/// Included as an accuracy reference, not a speed improvement.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DoubleIntermediary_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += (float)(v * OneEightyOverPiPrecise);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in double.RadiansToDegrees (IFloatingPointIeee754).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetRadiansToDegrees_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.RadiansToDegrees(v);
		return sum;
	}

	/// <summary>
	/// Current optimizer output: x * (180.0 / Math.PI).
	/// Constant folded to 57.29577951308232 and inlined at the call site.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentOptimizer_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += v * OneEightyOverPiD;
		return sum;
	}

	/// <summary>
	/// FMA(x, 180/pi, 0.0): fused multiply-add with zero addend.
	/// Expected to be identical to plain multiply in the JIT output.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FmaVariant_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Double.FusedMultiplyAdd(v, OneEightyOverPiD, 0.0);
		return sum;
	}
}



