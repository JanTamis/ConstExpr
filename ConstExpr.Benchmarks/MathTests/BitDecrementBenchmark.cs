using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Runtime.CompilerServices;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares float.BitDecrement / double.BitDecrement against custom bit-manipulation variants.
///
/// BitDecrement(x) returns the largest representable float/double strictly less than x.
/// For finite non-zero values this reduces to a one-instruction bit-pattern adjustment:
///   positive x → bits − 1  (smaller bit pattern = smaller positive value)
///   negative x → bits + 1  (larger bit pattern in sign-magnitude = more negative value)
///
/// Current optimizer output: emits the built-in call directly (no custom helper method).
///
/// Two type groups:
///   Float  – float.BitDecrement  vs FastBitDecrementV2(float)  vs FastBitDecrementV3(float)
///   Double – double.BitDecrement vs FastBitDecrementV2(double) vs FastBitDecrementV3(double)
///
/// V2 (float + double): float.IsFinite guard + branchless sign extraction via arithmetic shift.
///   (bits >> 31) | 1  gives +1 for positive bits, −1 for negative bits (no conditional move).
///   bits −= sign  →  bits − 1 for positive, bits + 1 for negative.
///   Combines NaN/+Inf/−Inf into a single IsFinite check; +0 handled by one extra compare.
///
/// V3 (float + double): identical logic to V2 but uses Unsafe.BitCast instead of BitConverter.
///   Unsafe.BitCast is a true zero-cost reinterpret (FMOV on ARM64); BitConverter.SingleToInt32Bits
///   is also intrinsified to FMOV, so V2 and V3 should compile to identical code — included as a
///   sanity-check to confirm that the BitConverter intrinsic is fully eliminated by the JIT.
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=0.996 ns | V2=0.649 ns (−35%) ← FASTEST | V3=0.658 ns (−34%)
///   Double: DotNet=0.988 ns | V2=0.652 ns (−34%) | V3=0.644 ns (−35%) ← FASTEST (within noise)
///
/// Conclusion: V2 ≈ V3 (both compile to the same code — BitConverter IS fully intrinsified).
///   Both are ~35% faster than the built-in for typical finite non-zero data.
///   The built-in uses a conditional move for the sign selection; the shift+OR trick in V2/V3
///   produces a tighter instruction sequence that the ARM64 JIT schedules more efficiently.
///   BitDecrementFunctionOptimizer has been updated to emit the V2 helper for float and double.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*BitDecrementBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BitDecrementBenchmark
{
	// 1 024 values uniformly distributed over [−4, 4].
	// All values are finite and non-zero in practice; the NaN/Inf/zero branches are never taken.
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
			var v = rng.NextDouble() * 8.0 - 4.0; // uniform in [−4, 4]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in float.BitDecrement — IFloatingPointIeee754 static method, inlined by the JIT.
	/// Reference baseline for float precision.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetBitDecrement_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.BitDecrement(v);
		return sum;
	}

	/// <summary>
	/// FastBitDecrementV2(float) — float.IsFinite guard + branchless sign trick.
	/// float.IsFinite compiles to a single unsigned-compare instruction on ARM64.
	/// (bits >> 31) | 1 extracts the sign as +1 / −1 without a branch; bits −= sign.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastBitDecrementV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastBitDecrementFloatV2(v);
		return sum;
	}

	/// <summary>
	/// FastBitDecrementV3(float) — same algorithm as V2 but reinterprets via Unsafe.BitCast.
	/// Confirms that Unsafe.BitCast and BitConverter.SingleToInt32Bits generate identical code.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastBitDecrementV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastBitDecrementFloatV3(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in double.BitDecrement — IFloatingPointIeee754 static method, inlined by the JIT.
	/// Reference baseline for double precision.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetBitDecrement_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.BitDecrement(v);
		return sum;
	}

	/// <summary>
	/// FastBitDecrementV2(double) — double.IsFinite guard + branchless sign trick.
	/// (bits >> 63) | 1L extracts the sign as +1 / −1 for long bits; bits −= sign.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastBitDecrementV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastBitDecrementDoubleV2(v);
		return sum;
	}

	/// <summary>
	/// FastBitDecrementV3(double) — same algorithm as V2 but reinterprets via Unsafe.BitCast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastBitDecrementV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastBitDecrementDoubleV3(v);
		return sum;
	}

	// ── implementations ──────────────────────────────────────────────────────

	/// <summary>
	/// float.IsFinite guard + branchless sign:
	///   (bits >> 31) | 1  →  +1 for positive bits, −1 for negative bits.
	///   bits −= sign  →  bits − 1 (positive) or bits + 1 (negative).
	/// NaN/±Inf handled by the IsFinite branch (rare; predicted not-taken on typical data).
	/// +0 (bits == 0) separately mapped to −epsilon (bits = 0x80000001).
	/// −0 (bits = 0x80000000 = negative int) correctly gives bits + 1 = 0x80000001 = −epsilon.
	/// </summary>
	private static float FastBitDecrementFloatV2(float x)
	{
		if (!float.IsFinite(x))
			return float.IsPositiveInfinity(x) ? float.MaxValue : x;

		var bits = BitConverter.SingleToInt32Bits(x);
		if (bits == 0) return -float.Epsilon;

		bits -= (bits >> 31) | 1;
		return BitConverter.Int32BitsToSingle(bits);
	}

	/// <summary>Same as V2 but uses Unsafe.BitCast for the float↔int reinterpretation.</summary>
	private static float FastBitDecrementFloatV3(float x)
	{
		if (!float.IsFinite(x))
			return float.IsPositiveInfinity(x) ? float.MaxValue : x;

		var bits = Unsafe.BitCast<float, int>(x);
		if (bits == 0) return -float.Epsilon;

		bits -= (bits >> 31) | 1;
		return Unsafe.BitCast<int, float>(bits);
	}

	/// <summary>
	/// double.IsFinite guard + branchless sign:
	///   (bits >> 63) | 1L  →  +1L for positive bits, −1L for negative bits.
	/// </summary>
	private static double FastBitDecrementDoubleV2(double x)
	{
		if (!double.IsFinite(x))
			return double.IsPositiveInfinity(x) ? double.MaxValue : x;

		var bits = BitConverter.DoubleToInt64Bits(x);
		if (bits == 0L) return -double.Epsilon;

		bits -= (bits >> 63) | 1L;
		return BitConverter.Int64BitsToDouble(bits);
	}

	/// <summary>Same as V2 but uses Unsafe.BitCast for the double↔long reinterpretation.</summary>
	private static double FastBitDecrementDoubleV3(double x)
	{
		if (!double.IsFinite(x))
			return double.IsPositiveInfinity(x) ? double.MaxValue : x;

		var bits = Unsafe.BitCast<double, long>(x);
		if (bits == 0L) return -double.Epsilon;

		bits -= (bits >> 63) | 1L;
		return Unsafe.BitCast<long, double>(bits);
	}
}



