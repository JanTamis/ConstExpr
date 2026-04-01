using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.Round / MathF.Round against several scalar alternatives (no SIMD).
///
/// Candidates tested:
///   DotNetRound        – Math.Round / MathF.Round (ToEven; single FRINTN/ROUNDSD instruction)
///   GenericMath        – double.Round / float.Round (IFloatingPoint&lt;T&gt; generic-math path)
///   AwayFromZero       – Math.Round(x, MidpointRounding.AwayFromZero) (FRINTA on ARM64)
///   FloorPlusHalf      – Math.Floor(x + 0.5) — rounds half-away-from-zero; 2 FP ops, differs semantically at -0.5 midpoints
///   IntCast            – (long/int)(x + 0.5) — branchless truncation trick; incorrect for negative midpoints
///   UnaryMinus groups  – Round(-x) direct vs -Round(x) (optimizer rewrite for unary-minus arguments)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT armv8.0-a):
///
///   Double group (baseline = DotNetRound_Double):
///     DotNetRound_Double    0.530 ns  ratio 1.00  ← WINNER: single FRINTN instruction
///     GenericMath_Double    0.547 ns  ratio 1.03  same instruction via IFloatingPoint&lt;T&gt;; marginal overhead
///     AwayFromZero_Double   0.556 ns  ratio 1.05  FRINTA instruction; negligible overhead
///     FloorPlusHalf_Double  0.589 ns  ratio 1.11  2 FP ops; 11% slower — avoid
///     LongCast_Double       0.672 ns  ratio 1.27  FP/int domain crossing — never use
///
///   Float group (baseline = DotNetRound_Float):
///     DotNetRound_Float     0.579 ns  ratio 1.00  ← WINNER (top three effectively tied)
///     GenericMath_Float     0.581 ns  ratio 1.00  same FRINTN via IFloatingPoint&lt;T&gt;
///     AwayFromZero_Float    0.581 ns  ratio 1.00  FRINTA; same throughput as ToEven for floats
///     FloorPlusHalf_Float   0.603 ns  ratio 1.04  marginal overhead
///     IntCast_Float         0.677 ns  ratio 1.17  domain crossing — avoid
///
///   UnaryMinus group (baseline = UnaryMinus_Direct = Math.Round(-x)):
///     UnaryMinus_Direct     0.594 ns  ratio 1.00
///     UnaryMinus_Rewritten  0.587 ns  ratio 0.99  within measurement noise — no real benefit
///
/// Conclusion:
///   Math.Round / MathF.Round is the fastest scalar implementation (single FRINTN instruction).
///   No alternative beats the hardware instruction.
///   The unary-minus rewrite Round(-x) → -Round(x) yields no measurable throughput benefit
///   (ratio 0.99, well within noise) and has been removed from RoundFunctionOptimizer.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*RoundBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class RoundBenchmark
{
	private const int N = 1_024;
	private float[] _floatData = null!;
	private double[] _doubleData = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			// Mix of positive/negative values with fractional parts, within safe range for cast impls
			// (|x| < 2^31 for int cast, |x| < 2^52 for long cast).
			var v = (rng.NextDouble() * 2.0 - 1.0) * 1e8;
			_floatData[i] = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in MathF.Round — single hardware instruction: FRINTN (ARM64) / ROUNDSS 0x8 (x64).
	/// Uses MidpointRounding.ToEven (banker's rounding).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetRound_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Round(v);
		return sum;
	}

	/// <summary>
	/// Generic-math path: float.Round via IFloatingPoint&lt;T&gt;.
	/// JIT devirtualises to the same FRINTN instruction as MathF.Round.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float GenericMath_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.Round(v);
		return sum;
	}

	/// <summary>
	/// Explicit away-from-zero rounding: MidpointRounding.AwayFromZero.
	/// ARM64: FRINTA instruction (round to nearest, ties away from zero).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float AwayFromZero_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Round(v, MidpointRounding.AwayFromZero);
		return sum;
	}

	/// <summary>
	/// Floor(x + 0.5) — rounds half-away-from-zero; 2 FP ops.
	/// Semantically different from ToEven at midpoints (e.g. 2.5 → 3 not 2).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FloorPlusHalf_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Floor(v + 0.5f);
		return sum;
	}

	/// <summary>
	/// Integer-cast trick: (int)(v + 0.5f). Only correct for positive values and |x| &lt; 2^31.
	/// Included as a lower-bound for branchless integer rounding speed.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float IntCast_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += (int)(v + 0.5f);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in Math.Round — single hardware instruction: FRINTN (ARM64) / ROUNDSD 0x8 (x64).
	/// Uses MidpointRounding.ToEven (banker's rounding).
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetRound_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Round(v);
		return sum;
	}

	/// <summary>
	/// Generic-math path: double.Round via IFloatingPoint&lt;T&gt;.
	/// JIT devirtualises to the same FRINTN instruction as Math.Round.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double GenericMath_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.Round(v);
		return sum;
	}

	/// <summary>
	/// Explicit away-from-zero rounding: MidpointRounding.AwayFromZero.
	/// ARM64: FRINTA instruction.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double AwayFromZero_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Round(v, MidpointRounding.AwayFromZero);
		return sum;
	}

	/// <summary>
	/// Floor(x + 0.5) — rounds half-away-from-zero; 2 FP ops.
	/// Semantically different from ToEven at midpoints (e.g. 2.5 → 3 not 2).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FloorPlusHalf_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Floor(v + 0.5);
		return sum;
	}

	/// <summary>
	/// Long-cast trick: (long)(v + 0.5). Only correct for positive values and |x| &lt; 2^52.
	/// Included to measure the FP→int domain-crossing overhead.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double LongCast_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += (long)(v + 0.5);
		return sum;
	}

	// ── Unary minus: Round(-x) direct vs -Round(x) (optimizer rewrite) ────

	/// <summary>
	/// Round(-x) — the pattern a user writes; optimizer rewrites this to -Round(x).
	/// Math.Round is an odd function: Round(-x) == -Round(x) for all finite values,
	/// so the rewrite is semantically safe.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("UnaryMinus")]
	public double UnaryMinus_Direct()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Round(-v);
		return sum;
	}

	/// <summary>
	/// -Round(x) — what RoundFunctionOptimizer rewrites the above to.
	/// Both paths have the same 2 FP ops (FNEG + FRINTN); order only affects
	/// whether the negation is the input or output of the rounding instruction.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("UnaryMinus")]
	public double UnaryMinus_Rewritten()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += -Math.Round(v);
		return sum;
	}
}


