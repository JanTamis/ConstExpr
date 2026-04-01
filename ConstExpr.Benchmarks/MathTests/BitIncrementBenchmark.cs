using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Runtime.CompilerServices;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares float.BitIncrement / double.BitIncrement against custom bit-manipulation variants.
///
/// BitIncrement(x) returns the smallest representable float/double strictly greater than x.
/// For finite non-zero values this reduces to a one-instruction bit-pattern adjustment:
///   positive x → bits + 1  (larger bit pattern = larger positive value)
///   negative x → bits − 1  (smaller bit pattern in sign-magnitude = less negative = larger value)
///
/// Current optimizer output: emits the built-in call directly (no custom helper method).
///
/// Two type groups:
///   Float  – float.BitIncrement  vs FastBitIncrementV2(float)  vs FastBitIncrementV3(float)
///   Double – double.BitIncrement vs FastBitIncrementV2(double) vs FastBitIncrementV3(double)
///
/// V2 (float + double): float.IsFinite guard + combined ±zero check + branchless sign extraction.
///   Both +0 and −0 map to +epsilon via a single zero-masked check: (bits &amp; INT_MAX) == 0.
///   (bits >> 31) | 1 gives +1 for positive bits, −1 for negative bits; bits += sign.
///   Compared to BitDecrement, BitIncrement needs to catch both ±0 (BitDecrement only needed +0);
///   the single masked-and check handles both without an extra branch.
///
/// V3 (float + double): identical logic to V2 but uses Unsafe.BitCast instead of BitConverter.
///   Sanity-check that BitConverter is fully intrinsified (FMOV on ARM64); V2 ≈ V3 expected.
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=0.817 ns | V2=0.654 ns (−20%) ← FASTEST | V3=0.656 ns (−20%)
///   Double: DotNet=0.807 ns | V2=0.652 ns (−19%) | V3=0.647 ns (−20%) ← FASTEST (within noise)
///
/// Conclusion: V2 ≈ V3 (BitConverter IS fully intrinsified — same FMOV as Unsafe.BitCast).
///   Both are ~20% faster than the built-in for typical finite non-zero data.
///   BitIncrementFunctionOptimizer has been updated to emit the V2 helper for float and double.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*BitIncrementBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BitIncrementBenchmark
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
	/// Built-in float.BitIncrement — IFloatingPointIeee754 static method, inlined by the JIT.
	/// Reference baseline for float precision.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetBitIncrement_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.BitIncrement(v);
		return sum;
	}

	/// <summary>
	/// FastBitIncrementV2(float) — float.IsFinite guard + combined ±zero check + branchless sign.
	/// (bits &amp; int.MaxValue) == 0 catches both +0 and −0 with a single masked compare.
	/// (bits >> 31) | 1 extracts +1 for positive bits, −1 for negative bits; bits += sign.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastBitIncrementV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastBitIncrementFloatV2(v);
		return sum;
	}

	/// <summary>
	/// FastBitIncrementV3(float) — same algorithm as V2 but reinterprets via Unsafe.BitCast.
	/// Confirms that Unsafe.BitCast and BitConverter.SingleToInt32Bits generate identical code.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastBitIncrementV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastBitIncrementFloatV3(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in double.BitIncrement — IFloatingPointIeee754 static method, inlined by the JIT.
	/// Reference baseline for double precision.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetBitIncrement_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.BitIncrement(v);
		return sum;
	}

	/// <summary>
	/// FastBitIncrementV2(double) — double.IsFinite guard + combined ±zero check + branchless sign.
	/// (bits &amp; long.MaxValue) == 0L catches both +0 and −0 with a single masked compare.
	/// (bits >> 63) | 1L extracts +1L for positive bits, −1L for negative bits; bits += sign.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastBitIncrementV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastBitIncrementDoubleV2(v);
		return sum;
	}

	/// <summary>
	/// FastBitIncrementV3(double) — same algorithm as V2 but reinterprets via Unsafe.BitCast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastBitIncrementV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastBitIncrementDoubleV3(v);
		return sum;
	}

	// ── implementations ──────────────────────────────────────────────────────

	/// <summary>
	/// float.IsFinite guard + combined ±zero check + branchless sign:
	///   (bits &amp; int.MaxValue) == 0 catches +0 (bits=0) and −0 (bits=int.MinValue) in one compare.
	///   Both return +epsilon (the smallest positive float).
	///   (bits >> 31) | 1 = +1 for positive bits, −1 for negative bits.
	///   bits += sign  →  bits + 1 (positive) or bits − 1 (negative).
	/// NaN/±Inf handled by the IsFinite branch (rare; predicted not-taken on typical data).
	/// −Inf → −MaxValue; NaN and +Inf unchanged.
	/// </summary>
	private static float FastBitIncrementFloatV2(float x)
	{
		if (!float.IsFinite(x))
			return float.IsNegativeInfinity(x) ? -float.MaxValue : x;

		var bits = BitConverter.SingleToInt32Bits(x);

		// Both +0 (bits=0) and −0 (bits=int.MinValue) → +epsilon (0x00000001)
		if ((bits & int.MaxValue) == 0) return float.Epsilon;

		// Branchless sign: (bits >> 31) | 1 = +1 for positive, −1 for negative
		bits += (bits >> 31) | 1;
		return BitConverter.Int32BitsToSingle(bits);
	}

	/// <summary>Same as V2 but uses Unsafe.BitCast for the float↔int reinterpretation.</summary>
	private static float FastBitIncrementFloatV3(float x)
	{
		if (!float.IsFinite(x))
			return float.IsNegativeInfinity(x) ? -float.MaxValue : x;

		var bits = Unsafe.BitCast<float, int>(x);

		if ((bits & int.MaxValue) == 0) return float.Epsilon;

		bits += (bits >> 31) | 1;
		return Unsafe.BitCast<int, float>(bits);
	}

	/// <summary>
	/// double.IsFinite guard + combined ±zero check + branchless sign:
	///   (bits &amp; long.MaxValue) == 0L catches +0 and −0 in one masked compare.
	///   (bits >> 63) | 1L = +1L for positive bits, −1L for negative bits; bits += sign.
	/// </summary>
	private static double FastBitIncrementDoubleV2(double x)
	{
		if (!double.IsFinite(x))
			return double.IsNegativeInfinity(x) ? -double.MaxValue : x;

		var bits = BitConverter.DoubleToInt64Bits(x);

		// Both +0 (bits=0L) and −0 (bits=long.MinValue) → +epsilon
		if ((bits & long.MaxValue) == 0L) return double.Epsilon;

		bits += (bits >> 63) | 1L;
		return BitConverter.Int64BitsToDouble(bits);
	}

	/// <summary>Same as V2 but uses Unsafe.BitCast for the double↔long reinterpretation.</summary>
	private static double FastBitIncrementDoubleV3(double x)
	{
		if (!double.IsFinite(x))
			return double.IsNegativeInfinity(x) ? -double.MaxValue : x;

		var bits = Unsafe.BitCast<double, long>(x);

		if ((bits & long.MaxValue) == 0L) return double.Epsilon;

		bits += (bits >> 63) | 1L;
		return Unsafe.BitCast<long, double>(bits);
	}
}


