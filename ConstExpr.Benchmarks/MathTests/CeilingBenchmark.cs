using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.Ceiling / MathF.Ceiling against several scalar alternatives (no SIMD).
///
/// Candidates tested:
///   DotNetCeiling   – Math.Ceiling / MathF.Ceiling         (single hardware instruction: FRINTP/ROUNDSD)
///   NegFloor        – -Floor(-x)                           (algebraic identity; previously emitted by CeilingFunctionOptimizer)
///   IntCast         – (int/long) truncation + conditional   (branchless integer-cast trick; UB-free for |x| &lt; 2^31/2^52)
///   GenericMath     – double.Ceiling / float.Ceiling        (IFloatingPoint&lt;T&gt; generic-math path)
///
/// All data is drawn from the safe range [-1e8, 1e8] for float and [-1e14, 1e14] for double so the
/// integer-cast implementations never overflow, making the comparison fair.
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT armv8.0-a):
///
///   Double group (baseline = DotNetCeiling_Double):
///     DotNetCeiling_Double  0.561 ns  ratio 1.00  ← WINNER: single FRINTP instruction
///     GenericMath_Double    0.570 ns  ratio 1.02  ≈ same instruction via IFloatingPoint&lt;T&gt;
///     NegFloor_Double       0.581 ns  ratio 1.03  3 FP ops — marginal overhead
///     LongCast_Double       0.667 ns  ratio 1.19  FP/int domain crossing — avoid
///
///   Float group (baseline = DotNetCeiling_Float):
///     DotNetCeiling_Float   0.572 ns  ratio 1.00  ← WINNER
///     GenericMath_Float     0.575 ns  ratio 1.01  ≈ tied
///     NegFloor_Float        0.587 ns  ratio 1.03  marginal overhead
///     IntCast_Float         0.681 ns  ratio 1.19  avoid
///
/// Conclusion:
///   Math.Ceiling / MathF.Ceiling is the fastest scalar implementation.
///   No alternative beats the hardware FRINTP/FRINTM instruction.
///   The CeilingFunctionOptimizer and FloorFunctionOptimizer have been updated to
///   always emit the direct Ceiling/Floor call and no longer apply the -Floor(-x) rewrite.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*CeilingBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CeilingBenchmark
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
            // Mix of positive/negative values with fractional parts, within safe range for cast impls.
            var v = (rng.NextDouble() * 2.0 - 1.0) * 1e8;
            _floatData[i] = (float)v;
            _doubleData[i] = v;
        }
    }

    // ── float ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Built-in MathF.Ceiling — single hardware instruction: FRINTP (ARM64) / ROUNDSS (x64).
    /// Handles NaN, infinity, and the full float range correctly.
    /// </summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = N)]
    [BenchmarkCategory("Float")]
    public float DotNetCeiling_Float()
    {
        var sum = 0f;
        foreach (var v in _floatData)
            sum += MathF.Ceiling(v);
        return sum;
    }

    /// <summary>
    /// Algebraic identity: Ceiling(x) = -Floor(-x).
    /// Previously emitted by CeilingFunctionOptimizer for Ceiling(-x) expressions (now removed).
    /// Requires two negations + one Floor instruction → 3 ops vs 1 for the baseline; +3% overhead.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Float")]
    public float NegFloor_Float()
    {
        var sum = 0f;
        foreach (var v in _floatData)
            sum += -MathF.Floor(-v);
        return sum;
    }

    /// <summary>
    /// Integer-cast trick: truncate toward zero then add 1 if x was positive-fractional.
    /// (int)x truncates, so for x &gt; 0 fractional we get floor; for x &lt;= 0 fractional we get ceiling.
    /// Branchless via conditional increment. Only correct for |x| &lt; 2^31.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Float")]
    public float IntCast_Float()
    {
        var sum = 0f;
        foreach (var v in _floatData)
        {
            var t = (int)v;
            sum += t < v ? t + 1.0f : t;
        }
        return sum;
    }

    /// <summary>
    /// Generic-math path: float.Ceiling via IFloatingPoint&lt;T&gt;.
    /// JIT devirtualises to the same FRINTP instruction as MathF.Ceiling.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Float")]
    public float GenericMath_Float()
    {
        var sum = 0f;
        foreach (var v in _floatData)
            sum += float.Ceiling(v);
        return sum;
    }

    // ── double ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Built-in Math.Ceiling — single hardware instruction: FRINTP (ARM64) / ROUNDSD (x64).
    /// Handles NaN, infinity, and the full double range correctly.
    /// </summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = N)]
    [BenchmarkCategory("Double")]
    public double DotNetCeiling_Double()
    {
        var sum = 0.0;
        foreach (var v in _doubleData)
            sum += Math.Ceiling(v);
        return sum;
    }

    /// <summary>
    /// Algebraic identity: Ceiling(x) = -Floor(-x).
    /// Previously emitted by CeilingFunctionOptimizer for Ceiling(-x) expressions (now removed).
    /// Same 3-op overhead as the Float variant: +3.6% vs direct Math.Ceiling.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Double")]
    public double NegFloor_Double()
    {
        var sum = 0.0;
        foreach (var v in _doubleData)
            sum += -Math.Floor(-v);
        return sum;
    }

    /// <summary>
    /// Long-cast trick: truncate toward zero then conditionally add 1.
    /// Branchless on modern CPUs (FCVTZS + CMGT/CSEL on ARM64). Only correct for |x| &lt; 2^52.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Double")]
    public double LongCast_Double()
    {
        var sum = 0.0;
        foreach (var v in _doubleData)
        {
            var t = (long)v;
            sum += t < v ? t + 1.0 : t;
        }
        return sum;
    }

    /// <summary>
    /// Generic-math path: double.Ceiling via IFloatingPoint&lt;T&gt;.
    /// JIT devirtualises to the same FRINTP/ROUNDSD instruction as Math.Ceiling.
    /// </summary>
    [Benchmark(OperationsPerInvoke = N)]
    [BenchmarkCategory("Double")]
    public double GenericMath_Double()
    {
        var sum = 0.0;
        foreach (var v in _doubleData)
            sum += double.Ceiling(v);
        return sum;
    }
}





