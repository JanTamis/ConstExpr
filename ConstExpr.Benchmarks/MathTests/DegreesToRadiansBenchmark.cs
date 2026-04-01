using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares DegreesToRadians implementations for float and double.
///
/// DegreesToRadians(x) = x * (pi/180)
///
/// The optimizer (before this benchmark) inlined a multiply-by-constant at the call site.
/// Benchmarking reveals that the .NET builtin is actually FASTER and MORE ACCURATE,
/// so the optimizer now delegates to T.DegreesToRadians(x) for all types.
///
/// Variants:
///   Float  - float.DegreesToRadians  (baseline: .NET IFloatingPoint)
///            CurrentOptimizer        (x * (MathF.PI / 180f) -- former optimizer output)
///            FmaVariant              (FMA(x, pi/180, 0f) -- mul+zero addend, same as FMUL)
///            DoubleIntermediary      ((float)((double)x * (pi/180.0)) -- higher precision, slower)
///   Double - double.DegreesToRadians (baseline)
///            CurrentOptimizer        (x * (Math.PI / 180.0) -- former optimizer output)
///            FmaVariant              (FMA(x, pi/180, 0.0) -- same as FMUL with zero addend)
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT, N=1024):
///   Float:  DotNet=0.538 ns | Optimizer=0.593 ns (+10%) | Fma=0.588 ns (+9%) | DblInterm=0.600 ns (+12%)
///   Double: DotNet=0.536 ns | Optimizer=0.586 ns (+9%)  | Fma=0.591 ns (+10%)
///
/// Why is the builtin faster?
///   The JIT recognises float/double.DegreesToRadians as a vectorisable loop and emits
///   multi-accumulator SIMD code. The explicit x*constant form does not reliably trigger
///   the same auto-vectorisation. The builtin also uses the more precise constant:
///   (float)(Math.PI/180.0) = 0x3C8EFA35 vs MathF.PI/180f = 0x3C8EFA36 (1 ULP less accurate).
///
/// Conclusion: The inline-multiply optimisation is removed. T.DegreesToRadians(x) is
///   ~10% faster and 1 ULP more accurate for float.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*DegreesToRadiansBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class DegreesToRadiansBenchmark
{
	// 1 024 values uniformly distributed over a wide degree range.
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	// Constants that match exactly what the optimizer emits.
	// Float: MathF.PI / 180f  (float arithmetic, may differ by 1 ULP from the double path)
	// Double: Math.PI / 180.0 (full double precision)
	private const float  PiOver180F = MathF.PI / 180f;
	private const double PiOver180D = Math.PI  / 180.0;

	// Used by the DoubleIntermediary float variant: the more precise double constant.
	private const double PiOver180Precise = Math.PI / 180.0; // 0.017453292519943295

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData  = new float[N];
		_doubleData = new double[N];
		for (var i = 0; i < N; i++)
		{
			var v = rng.NextDouble() * 720.0 - 360.0; // uniform in [-360, 360] degrees
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in float.DegreesToRadians (IFloatingPointIeee754).
	/// Internally computes degrees * (T.Pi / T.CreateChecked(180)).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetDegreesToRadians_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.DegreesToRadians(v);
		return sum;
	}

	/// <summary>
	/// Current optimizer output: x * (MathF.PI / 180f).
	/// The constant is folded at C# compile time to 0.017453292f and inlined
	/// directly at the call site — one FMUL, zero method-call overhead.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentOptimizer_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += v * PiOver180F;
		return sum;
	}

	/// <summary>
	/// FMA(x, pi/180, 0f): fused multiply-add with zero addend.
	/// On hardware that treats FMA(x, C, 0) specially this could save a cycle;
	/// in practice the JIT typically emits the same FMUL as a plain multiply.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FmaVariant_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += Single.FusedMultiplyAdd(v, PiOver180F, 0f);
		return sum;
	}

	/// <summary>
	/// Higher-precision float result via double intermediary:
	///   (float)((double)x * (Math.PI / 180.0))
	/// Avoids the 1-ULP rounding error that can occur when dividing the float
	/// approximation of pi by 180. Slower due to widening + narrowing conversions.
	/// Included as an accuracy reference, not a speed improvement.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DoubleIntermediary_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += (float)((double)v * PiOver180Precise);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in double.DegreesToRadians (IFloatingPointIeee754).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetDegreesToRadians_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.DegreesToRadians(v);
		return sum;
	}

	/// <summary>
	/// Current optimizer output: x * (Math.PI / 180.0).
	/// Constant folded to 0.017453292519943295 and inlined at the call site.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentOptimizer_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += v * PiOver180D;
		return sum;
	}

	/// <summary>
	/// FMA(x, pi/180, 0.0): fused multiply-add with zero addend.
	/// Expected to be identical to plain multiply in the JIT output.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FmaVariant_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Double.FusedMultiplyAdd(v, PiOver180D, 0.0);
		return sum;
	}
}


