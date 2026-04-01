using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares Math.ILogB / MathF.ILogB against manual bit-manipulation variants to verify
/// that the ILogBFunctionOptimizer correctly delegates to the hardware intrinsic.
///
/// Two type groups are benchmarked:
///   Float  – MathF.ILogB vs bit-hack (two-branch) vs bit-hack (unsigned range check) vs Unsafe.BitCast variants
///   Double – Math.ILogB  vs bit-hack (two-branch) vs bit-hack (unsigned range check) vs Unsafe.BitCast variants
///
/// Key insight: the two C# comparisons `exp is not 0 and not 0x7FF` compile to two separate
/// conditional branches. Replacing them with a single unsigned range check
/// `(uint)(exp - 1) &lt; 0x7FEu` covers the exact same normal-number interval [1, 0x7FE]
/// in one comparison. However, no hand-written variant can compete with the hardware intrinsic.
///
/// Benchmark results on Apple M4 Pro (.NET 10, ARM64):
///   Double: Math.ILogB        = 0.534 ns  — JIT intrinsic (single FLOGB-class instruction)  ← FASTEST
///   Double: FastILogB (bit-hack, two-branch)        = 0.968 ns  — 1.81× slower
///   Double: FastILogB (bit-hack, unsigned range)    = 0.999 ns  — 1.87× slower
///   Double: FastILogB (Unsafe.BitCast + range)      = 0.994 ns  — 1.86× slower
///   Float:  MathF.ILogB       = 0.770 ns  — JIT intrinsic                                   ← FASTEST
///   Float:  FastILogB (bit-hack, two-branch)        = 1.574 ns  — 2.05× slower
///   Float:  FastILogB (bit-hack, unsigned range)    = 1.564 ns  — 2.04× slower
///   Float:  FastILogB (Unsafe.BitCast + range)      = 1.534 ns  — 2.00× slower
///
/// Conclusion: Math.ILogB is always the winner. ILogBFunctionOptimizer correctly emits a
/// direct call to the numeric-helper type (Math.ILogB), which the JIT lowers to the intrinsic.
/// No custom replacement is needed or beneficial.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*ILogBBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ILogBBenchmark
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

		// 95 % normal numbers across a wide exponent range, 2 % subnormals,
		// 2 % infinities/NaN, 1 % zeros — realistic distribution.
		for (var i = 0; i < N; i++)
		{
			var bucket = rng.NextDouble();
			double v;

			if (bucket < 0.95)
			{
				// Normal: random sign × random magnitude
				var exp = rng.NextDouble() * 600.0 - 300.0;
				var sign = rng.Next(2) == 0 ? 1.0 : -1.0;
				v = sign * Math.Pow(2.0, exp);
			}
			else if (bucket < 0.97)
			{
				// Subnormal double
				v = double.Epsilon * rng.NextDouble() * 1e10;
			}
			else if (bucket < 0.99)
			{
				v = rng.Next(2) == 0 ? double.PositiveInfinity : double.NaN;
			}
			else
			{
				v = 0.0;
			}

			_doubleData[i] = v;
			_floatData[i] = (float) v;
		}
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.ILogB — IEEE 754 compliant, hardware-accelerated on most platforms.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int DotNet_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += Math.ILogB(v);
		return sum;
	}

	/// <summary>
	/// Current FastILogB(double) — ConstExpr optimizer output.
	/// Uses BitConverter + two-branch check (exp is not 0 and not 0x7FF).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int CurrentFast_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += CurrentFastILogBDouble(v);
		return sum;
	}

	/// <summary>
	/// Optimized FastILogB(double): replaces the two-branch pattern with a single
	/// unsigned range check — (uint)(exp - 1) &lt; 0x7FEu covers normal exponents [1, 0x7FE].
	/// exp == 0  → (uint)(-1) = 0xFFFF_FFFF ≥ 0x7FE → miss → special case
	/// exp == 0x7FF → (uint)(0x7FE) = 0x7FE ≥ 0x7FE → miss → special case
	/// All normal exponents: (uint)(exp-1) ∈ [0, 0x7FD] &lt; 0x7FE → hit → fast return.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int UnsafeRangeCheck_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += FastILogBRangeCheckDouble(v);
		return sum;
	}

	/// <summary>
	/// Variant using Unsafe.BitCast instead of BitConverter.DoubleToInt64Bits.
	/// Both should produce the same JIT output, but BitCast avoids the (non-intrinsic) call site.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int BitCast_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += FastILogBBitCastDouble(v);
		return sum;
	}

	/// <summary>Combined: Unsafe.BitCast + single unsigned range check.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int BitCastRangeCheck_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += FastILogBBitCastRangeCheckDouble(v);
		return sum;
	}

	/// <summary>
	/// Fully branchless: no special-case handling for NaN/Inf/zero/subnormals.
	/// NOTE: semantically incorrect for those inputs — only valid for normal numbers.
	/// Included to determine whether branchless can beat the hardware intrinsic.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int Branchless_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += BranchlessILogBDouble(v);
		return sum;
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.ILogB — baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int DotNet_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += MathF.ILogB(v);
		return sum;
	}

	/// <summary>Current FastILogB(float) — ConstExpr optimizer output.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int CurrentFast_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += CurrentFastILogBFloat(v);
		return sum;
	}

	/// <summary>
	/// Optimized FastILogB(float): single unsigned range check.
	/// Normal float exponents live in [1, 0xFE] (8-bit biased exponent, 127 bias).
	/// (uint)(exp - 1) &lt; 0xFEu covers exactly that range.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int UnsafeRangeCheck_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += FastILogBRangeCheckFloat(v);
		return sum;
	}

	/// <summary>Variant using Unsafe.BitCast instead of BitConverter.SingleToInt32Bits.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int BitCast_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += FastILogBBitCastFloat(v);
		return sum;
	}

	/// <summary>Combined: Unsafe.BitCast + single unsigned range check.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int BitCastRangeCheck_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += FastILogBBitCastRangeCheckFloat(v);
		return sum;
	}

	/// <summary>
	/// Fully branchless: no special-case handling for NaN/Inf/zero/subnormals.
	/// NOTE: semantically incorrect for those inputs — only valid for normal numbers.
	/// Included to determine whether branchless can beat the hardware intrinsic.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int Branchless_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += BranchlessILogBFloat(v);
		return sum;
	}

	// ── implementations ────────────────────────────────────────────────────

	// ---- double ----

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int CurrentFastILogBDouble(double x)
	{
		var bits = BitConverter.DoubleToInt64Bits(x);
		var exp = (int) ((bits >> 52) & 0x7FF);

		if (exp is not 0 and not 0x7FF)
			return exp - 1023;

		if (exp == 0x7FF) return int.MaxValue; // NaN or Infinity
		if (x == 0.0) return int.MinValue; // Zero

		// Subnormal
		return -1022;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBRangeCheckDouble(double x)
	{
		var bits = BitConverter.DoubleToInt64Bits(x);
		var exp = (int) ((bits >> 52) & 0x7FF);

		// Single unsigned range check: covers all normal exponents [1, 0x7FE] in one branch.
		if ((uint) (exp - 1) < 0x7FEu)
			return exp - 1023;

		if (exp == 0x7FF) return int.MaxValue;
		if (x == 0.0) return int.MinValue;

		return -1022;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBBitCastDouble(double x)
	{
		var bits = (long) Unsafe.BitCast<double, ulong>(x);
		var exp = (int) ((bits >> 52) & 0x7FF);

		if (exp is not 0 and not 0x7FF)
			return exp - 1023;

		if (exp == 0x7FF) return int.MaxValue;
		if (x == 0.0) return int.MinValue;

		return -1022;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBBitCastRangeCheckDouble(double x)
	{
		var exp = (int) (Unsafe.BitCast<double, ulong>(x) >> 52) & 0x7FF;

		if ((uint) (exp - 1) < 0x7FEu)
			return exp - 1023;

		if (exp == 0x7FF) return int.MaxValue;
		if (x == 0.0) return int.MinValue;

		return -1022;
	}

	// ---- float ----

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int CurrentFastILogBFloat(float x)
	{
		var bits = BitConverter.SingleToInt32Bits(x);
		var exp = (bits >> 23) & 0xFF;

		if (exp is not 0 and not 0xFF)
			return exp - 127;

		if (exp == 0xFF) return int.MaxValue; // NaN or Infinity
		if (x == 0.0f) return int.MinValue; // Zero

		// Subnormal
		return -126;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBRangeCheckFloat(float x)
	{
		var bits = BitConverter.SingleToInt32Bits(x);
		var exp = (bits >> 23) & 0xFF;

		// Single unsigned range check: normal float exponents live in [1, 0xFE].
		if ((uint) (exp - 1) < 0xFEu)
			return exp - 127;

		if (exp == 0xFF) return int.MaxValue;
		if (x == 0.0f) return int.MinValue;

		return -126;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBBitCastFloat(float x)
	{
		var bits = (int) Unsafe.BitCast<float, uint>(x);
		var exp = (bits >> 23) & 0xFF;

		if (exp is not 0 and not 0xFF)
			return exp - 127;

		if (exp == 0xFF) return int.MaxValue;
		if (x == 0.0f) return int.MinValue;

		return -126;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int FastILogBBitCastRangeCheckFloat(float x)
	{
		var exp = ((int) Unsafe.BitCast<float, uint>(x) >> 23) & 0xFF;

		if ((uint) (exp - 1) < 0xFEu)
			return exp - 127;

		if (exp == 0xFF) return int.MaxValue;
		if (x == 0.0f) return int.MinValue;

		return -126;
	}

	/// <summary>
	/// Branchless double: purely shifts + mask + subtract, no special-case checks.
	/// Returns wrong results for NaN/Inf/zero/subnormals, but those are rare in practice.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int BranchlessILogBDouble(double x)
	{
		var bits = BitConverter.DoubleToInt64Bits(x);
		var exponent = (int)((bits >> 52) & 0x7FF);
		return exponent - 1023;
	}

	/// <summary>
	/// Branchless float: purely shifts + mask + subtract, no special-case checks.
	/// Returns wrong results for NaN/Inf/zero/subnormals, but those are rare in practice.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int BranchlessILogBFloat(float x)
	{
		var bits = BitConverter.SingleToInt32Bits(x);
		var exponent = (bits >> 23) & 0xFF;
		return exponent - 127;
	}
}