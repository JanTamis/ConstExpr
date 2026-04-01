using BenchmarkDotNet.Attributes;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar absolute-value implementations for int.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*AbsBenchmark*'
/// </summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 2, printSource: true)]
public class AbsBenchmark
{
	// 1 024 random ints (fixed seed for reproducibility).
	// Instance field set in [GlobalSetup] so the JIT cannot see the values
	// at compile / JIT time and is forced to emit real loads per iteration.
	private const int N = 1_024;
	private int[] _data = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_data = new int[N];
		for (var i = 0; i < N; i++)
			// Exclude int.MinValue: AbsFast overflows on negation there.
			_data[i] = rng.Next(int.MinValue + 1, int.MaxValue);
	}

	// ── benchmarks ─────────────────────────────────────────────────────────

	/// <summary>Math.Abs — JIT: single ABS W,W per element on ARM64.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	public int DotNetAbs()
	{
		var sum = 0;
		foreach (var v in _data)
			sum += Math.Abs(v);
		return sum;
	}

	/// <summary>
	/// Branchless bit-manipulation — current ConstExpr generator output.
	/// ARM64: ASR W1,W0,#31  /  ADD W0,W0,W1  /  EOR W0,W0,W1  (3 instructions).
	/// Also has undefined behaviour for int.MinValue.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	public int AbsFast()
	{
		var sum = 0;

		foreach (var v in _data)
		{
			var mask = v >> 31;
			sum += (v + mask) ^ mask;
		}
		return sum;
	}

	/// <summary>
	/// Conditional expression — no branch penalty on ARM64.
	/// JIT: CMP W0,#0  /  CNEG W0,W0,lt  (2 instructions).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	public int Ternary()
	{
		var sum = 0;
		foreach (var v in _data)
			sum += v < 0 ? -v : v;
		return sum;
	}

	/// <summary>
	/// Proposed replacement: int.Abs(v) via INumber&lt;T&gt;.
	/// JIT devirtualises to a single ABS instruction — same throughput as
	/// Math.Abs and the correct generic-code pattern for the source generator.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	public int GenericMathAbs()
	{
		var sum = 0;
		foreach (var v in _data)
			sum += int.Abs(v);
		return sum;
	}
}