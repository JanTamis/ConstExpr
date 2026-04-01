using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar ScaleB implementations for float and double.
///
/// Four implementations are benchmarked per category:
///   DotNet      – Math.ScaleB / MathF.ScaleB (baseline, hardware-accurate)
///   Current     – current ConstExpr optimizer output: three-scale BitConverter technique.
///                 Uses up to 4 conditional pre-scale multiplications to bring n into the
///                 normal exponent range, then encodes 2^n via BitConverter.
///   Unsafe      – identical three-scale logic, but uses Unsafe.As instead of BitConverter
///                 for the runtime-variable reinterpret step.  The JIT can fold the constant
///                 pre-scale multipliers in both variants, so the measurable difference is in
///                 the final encode: BitConverter intrinsic vs direct stack-reinterpret.
///   FastPath    – single-step direct encode (Unsafe.As) when n is inside the normal exponent
///                 range (float: [-126, 127], double: [-1022, 1023]).  For extreme n values,
///                 falls back to the built-in Math.ScaleB.  Eliminates all four branches for
///                 the common case.
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method               Category  Mean      Ratio  Note
///   -------------------  --------  --------  -----  ------------------------------------------
///   DotNetScaleB_Float   Float     0.658 ns  1.00x  built-in, accurate
///   CurrentScaleB_Float  Float     0.704 ns  1.07x  three-scale BitConverter (previous output)
///   UnsafeScaleB_Float   Float     0.702 ns  1.07x  three-scale Unsafe.As — identical to Current
///   FastPathScaleB_Float Float     0.655 ns  0.99x  ← single-branch, ties DotNet
///
///   DotNetScaleB_Double  Double    0.655 ns  1.00x  built-in, accurate
///   CurrentScaleB_Double Double    0.724 ns  1.11x  three-scale BitConverter (previous output)
///   UnsafeScaleB_Double  Double    0.726 ns  1.11x  three-scale Unsafe.As — identical to Current
///   FastPathScaleB_Double Double   0.666 ns  1.02x  ← single-branch, 8 % faster than Current
///
/// Winner: FastPath — eliminates all four pre-scale branches for n in the normal exponent range.
/// The optimizer has been updated to emit the FastPath implementation.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*ScaleBBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ScaleBBenchmark
{
	// 1 024 (x, n) pairs.
	// x is uniform in [-1, 1] (normalised mantissa range).
	// n is uniform in [-50, 50] — the typical ScaleB use-case; all four conditional
	// branches in the three-scale implementation are never taken, so we measure the
	// hot path exclusively.
	private const int N = 1_024;

	private float[]  _floatX  = null!;
	private int[]    _floatN  = null!;
	private double[] _doubleX = null!;
	private int[]    _doubleN = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatX  = new float[N];
		_floatN  = new int[N];
		_doubleX = new double[N];
		_doubleN = new int[N];

		for (var i = 0; i < N; i++)
		{
			var x = rng.NextDouble() * 2.0 - 1.0; // [-1, 1]
			var n = rng.Next(-50, 51);             // typical exponent shift
			_floatX[i]  = (float)x;
			_floatN[i]  = n;
			_doubleX[i] = x;
			_doubleN[i] = n;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.ScaleB — managed but hardware-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetScaleB_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += MathF.ScaleB(_floatX[i], _floatN[i]);
		return sum;
	}

	/// <summary>
	/// Current ConstExpr optimizer output.
	/// Three-scale technique: up to 4 conditional pre-scale multiplications bring n into
	/// [-126, 127], then 2^n is encoded directly via BitConverter.Int32BitsToSingle.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentScaleB_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += ThreeScaleBitConverterFloat(_floatX[i], _floatN[i]);
		return sum;
	}

	/// <summary>
	/// Three-scale with Unsafe.As for the runtime-variable reinterpret.
	/// Identical branch structure to Current; the only difference is that the final
	/// (n + 127) &lt;&lt; 23 → float reinterpretation uses Unsafe.As instead of BitConverter.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float UnsafeScaleB_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += ThreeScaleUnsafeFloat(_floatX[i], _floatN[i]);
		return sum;
	}

	/// <summary>
	/// Fast-path implementation: a single Unsafe.As encode when n is in [-126, 127].
	/// Falls back to MathF.ScaleB for extreme exponents.
	/// Eliminates all four conditional branches for the common case.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastPathScaleB_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += FastPathFloat(_floatX[i], _floatN[i]);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.ScaleB — managed but hardware-accurate.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetScaleB_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += Math.ScaleB(_doubleX[i], _doubleN[i]);
		return sum;
	}

	/// <summary>Current ConstExpr optimizer output (three-scale BitConverter).</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentScaleB_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += ThreeScaleBitConverterDouble(_doubleX[i], _doubleN[i]);
		return sum;
	}

	/// <summary>Three-scale with Unsafe.As for the runtime-variable reinterpret step.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double UnsafeScaleB_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += ThreeScaleUnsafeDouble(_doubleX[i], _doubleN[i]);
		return sum;
	}

	/// <summary>
	/// Fast-path: single Unsafe.As encode for n in [-1022, 1023]; Math.ScaleB fallback.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastPathScaleB_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += FastPathDouble(_doubleX[i], _doubleN[i]);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	/// <summary>
	/// Current three-scale technique (mirrored from ScaleBFunctionOptimizer).
	/// Uses up to two pre-scale multiplications before the final IEEE 754 exponent encode.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float ThreeScaleBitConverterFloat(float x, int n)
	{
		if (n > 254)  { x *= BitConverter.Int32BitsToSingle(0x7F000000); n -= 127; } // × 2^127
		if (n < -252) { x *= BitConverter.Int32BitsToSingle(0x00800000); n += 126; } // × 2^-126
		if (n > 127)  { x *= BitConverter.Int32BitsToSingle(0x7F000000); n -= 127; }
		if (n < -126) { x *= BitConverter.Int32BitsToSingle(0x00800000); n += 126; }
		return x * BitConverter.Int32BitsToSingle((n + 127) << 23);
	}

	/// <summary>
	/// Three-scale with Unsafe.As for the runtime-variable reinterpret.
	/// The constant pre-scale multipliers are still encoded as BitConverter literals
	/// (JIT folds them to constant floats); only the final runtime-variable encode changes.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float ThreeScaleUnsafeFloat(float x, int n)
	{
		if (n > 254)  { x *= BitConverter.Int32BitsToSingle(0x7F000000); n -= 127; }
		if (n < -252) { x *= BitConverter.Int32BitsToSingle(0x00800000); n += 126; }
		if (n > 127)  { x *= BitConverter.Int32BitsToSingle(0x7F000000); n -= 127; }
		if (n < -126) { x *= BitConverter.Int32BitsToSingle(0x00800000); n += 126; }
		var bits = (n + 127) << 23;
		return x * Unsafe.As<int, float>(ref bits);
	}

	/// <summary>
	/// Fast-path: direct single-step encode when n is in the normal float exponent range.
	/// Condition: (uint)(n + 126) &lt;= 253u  ←→  n ∈ [-126, 127].
	/// Falls back to MathF.ScaleB for subnormal, overflow, or extreme-exponent inputs.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastPathFloat(float x, int n)
	{
		if ((uint)(n + 126) <= 253u)
		{
			var bits = (n + 127) << 23;
			return x * Unsafe.As<int, float>(ref bits);
		}
		return MathF.ScaleB(x, n);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ThreeScaleBitConverterDouble(double x, int n)
	{
		if (n > 2046)  { x *= BitConverter.UInt64BitsToDouble(0x7FE0000000000000UL); n -= 1023; } // × 2^1023
		if (n < -2044) { x *= BitConverter.UInt64BitsToDouble(0x0010000000000000UL); n += 1022; } // × 2^-1022
		if (n > 1023)  { x *= BitConverter.UInt64BitsToDouble(0x7FE0000000000000UL); n -= 1023; }
		if (n < -1022) { x *= BitConverter.UInt64BitsToDouble(0x0010000000000000UL); n += 1022; }
		return x * BitConverter.UInt64BitsToDouble((ulong)((long)(n + 1023) << 52));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ThreeScaleUnsafeDouble(double x, int n)
	{
		if (n > 2046)  { x *= BitConverter.UInt64BitsToDouble(0x7FE0000000000000UL); n -= 1023; }
		if (n < -2044) { x *= BitConverter.UInt64BitsToDouble(0x0010000000000000UL); n += 1022; }
		if (n > 1023)  { x *= BitConverter.UInt64BitsToDouble(0x7FE0000000000000UL); n -= 1023; }
		if (n < -1022) { x *= BitConverter.UInt64BitsToDouble(0x0010000000000000UL); n += 1022; }
		var bits = (ulong)((long)(n + 1023) << 52);
		return x * Unsafe.As<ulong, double>(ref bits);
	}

	/// <summary>
	/// Fast-path: direct single-step encode when n is in the normal double exponent range.
	/// Condition: (uint)(n + 1022) &lt;= 2045u  ←→  n ∈ [-1022, 1023].
	/// Falls back to Math.ScaleB for extreme exponents.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastPathDouble(double x, int n)
	{
		if ((uint)(n + 1022) <= 2045u)
		{
			var bits = (ulong)((long)(n + 1023) << 52);
			return x * Unsafe.As<ulong, double>(ref bits);
		}
		return Math.ScaleB(x, n);
	}
}


