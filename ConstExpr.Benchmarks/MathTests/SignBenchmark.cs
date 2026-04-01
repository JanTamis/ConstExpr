using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Sign implementations for int, float, and double.
///
/// Three groups:
///   Int    – Math.Sign  vs BranchlessBitTrick  vs Ternary  vs GenericINumber
///   Float  – Math.Sign  vs OldFastSign (Int32.CopySign)  vs BitOrShift (current)  vs UnsafeBitOrShift  vs Branchless  vs GenericINumber
///   Double – Math.Sign  vs OldFastSign (Double.CopySign) vs BitOrShift (current)  vs UnsafeBitOrShift  vs Branchless  vs GenericINumber
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Double: DotNet=0.522ns  OldFastSign=0.292ns(-44%)  BitOrShift=0.289ns(-45%)  Unsafe=0.333ns  Branchless=0.303ns  Generic=0.758ns(+45%)
///   Float:  DotNet=0.530ns  OldFastSign=0.489ns(-8%)   BitOrShift=0.289ns(-45%)  Unsafe=0.334ns  Branchless=0.310ns  Generic=0.760ns(+43%)
///   Int:    DotNet=0.284ns  Branchless=0.283ns(same)   Ternary=0.262ns(-8%)                                          Generic=0.479ns(+69%)
///
/// Winner: BitOrShift for float (+45% vs Math.Sign) and double (+45% vs Math.Sign).
///
/// Generic INumber&lt;T&gt; analysis:
///   Even with JIT devirtualisation in Release, the generic path is 2.6× slower than
///   BitOrShift for float/double, and 69 % slower than Math.Sign for int.
///   Cost breakdown: T.IsZero (FP zero-check), T.CopySign (FP intrinsic dispatch),
///   Int32.CreateChecked (checked FP→int with exception path).
///   Advantage: works for any T : INumber&lt;T&gt; with a single implementation.
///
/// Unsafe.As is slower than BitConverter (JIT intrinsifies BitConverter.Single/DoubleToXBits better).
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SignBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SignBenchmark
{
	private const int N = 1_024;

	private int[]    _intData    = null!;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_intData    = new int[N];
		_floatData  = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			_intData[i] = rng.Next(int.MinValue, int.MaxValue);
			var d = rng.NextDouble() * 200.0 - 100.0;
			_floatData[i]  = (float)d;
			_doubleData[i] = d;
		}

		// ~3 % zeros to exercise the zero-check path realistically.
		for (var i = 0; i < N; i += 32)
		{
			_intData[i]    = 0;
			_floatData[i]  = 0.0f;
			_doubleData[i] = 0.0;
		}
	}

	// ── int ────────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Sign(int) — baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Int")]
	public int DotNetSign_Int()
	{
		var sum = 0;
		foreach (var v in _intData)
			sum += Math.Sign(v);
		return sum;
	}

	/// <summary>
	/// Branchless bit-trick: (x >> 31) | (int)((uint)-x >> 31).
	/// ARM64: two ASR/LSR + one ORR — 3 instructions, no branch.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Int")]
	public int BranchlessSign_Int()
	{
		var sum = 0;
		foreach (var v in _intData)
			sum += SignBranchlessInt(v);
		return sum;
	}

	/// <summary>
	/// Ternary: (x > 0 ? 1 : 0) - (x &lt; 0 ? 1 : 0).
	/// JIT emits two CMP + two CSEL on ARM64.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Int")]
	public int TernarySign_Int()
	{
		var sum = 0;
		foreach (var v in _intData)
			sum += (v > 0 ? 1 : 0) - (v < 0 ? 1 : 0);
		return sum;
	}

	/// <summary>
	/// Generic INumber: Math.Sign(v) via INumber interface.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Int")]
	public int GenericSign_Int()
	{
		var sum = 0;
		foreach (var v in _intData)
			sum += GenericSign(v);
		return sum;
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Sign(float) — baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int DotNetSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += Math.Sign(v);
		return sum;
	}

	/// <summary>
	/// Old ConstExpr FastSign (pre-benchmark): zero-check then Int32.CopySign(1, SingleToInt32Bits(x)).
	/// Uses generic-numeric Int32.CopySign — one BitConverter round-trip + one integer CopySign.
	/// Only 8 % faster than Math.Sign; replaced by BitOrShift after benchmarking.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int OldFastSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += OldFastSignFloat(v);
		return sum;
	}

	/// <summary>
	/// Current ConstExpr FastSign (post-benchmark): zero-check then "1 | (bits >> 31)".
	/// 45 % faster than Math.Sign; 41 % faster than the old Int32.CopySign approach.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int CurrentFastSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += BitOrShiftSignFloat(v);
		return sum;
	}

	/// <summary>
	/// BitOrShift: zero-check then "1 | (bits >> 31)" via BitConverter.
	/// Positive: 1|0=1  Negative: 1|-1=-1  No FP arithmetic beyond the zero compare.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int BitOrShiftSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += BitOrShiftSignFloat(v);
		return sum;
	}

	/// <summary>
	/// UnsafeBitOrShift: same as BitOrShift but via Unsafe.As — zero BitConverter overhead;
	/// gives the JIT a single FMOV + ASR + ORR view of the operation.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int UnsafeBitOrShiftSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += UnsafeBitOrShiftSignFloat(v);
		return sum;
	}

	/// <summary>
	/// Branchless: zero-check folded into mask multiply — fully branch-free.
	/// nonZero = (absBits != 0) ? 1 : 0  then  (1 | signBit) * nonZero.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int BranchlessSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += BranchlessSignFloat(v);
		return sum;
	}

	/// <summary>
	/// Generic INumber: Math.Sign(v) via INumber interface.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public int GenericSign_Float()
	{
		var sum = 0;
		foreach (var v in _floatData)
			sum += GenericSign(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Sign(double) — baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int DotNetSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += Math.Sign(v);
		return sum;
	}

	/// <summary>
	/// Old ConstExpr FastSign (pre-benchmark): zero-check then (int)Double.CopySign(1.0, x).
	/// CopySign is a hardware intrinsic; the int cast is FCVTZS on ARM64.
	/// Already 44 % faster than Math.Sign, but replaced by BitOrShift for consistency.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int OldFastSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += OldFastSignDouble(v);
		return sum;
	}

	/// <summary>
	/// Current ConstExpr FastSign (post-benchmark): zero-check then "1 | (int)(bits >> 63)".
	/// 45 % faster than Math.Sign; consistent with the float implementation.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int CurrentFastSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += BitOrShiftSignDouble(v);
		return sum;
	}

	/// <summary>
	/// BitOrShift: zero-check then "1 | (int)(bits >> 63)" via BitConverter.
	/// Avoids FP pipeline entirely after the initial FCMP.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int BitOrShiftSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += BitOrShiftSignDouble(v);
		return sum;
	}

	/// <summary>
	/// UnsafeBitOrShift: same as BitOrShift but via Unsafe.As&lt;double, long&gt;.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int UnsafeBitOrShiftSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += UnsafeBitOrShiftSignDouble(v);
		return sum;
	}

	/// <summary>
	/// Branchless: fully branch-free using long bit mask multiply.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int BranchlessSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += BranchlessSignDouble(v);
		return sum;
	}

	/// <summary>
	/// Generic INumber: Math.Sign(v) via INumber interface.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public int GenericSign_Double()
	{
		var sum = 0;
		foreach (var v in _doubleData)
			sum += GenericSign(v);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int SignBranchlessInt(int x) =>
		(x >> 31) | (int)((uint)-x >> 31);

	// Float ─────────────────────────────────────────────────────────────────

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int OldFastSignFloat(float x)
	{
		if (x == 0.0f) return 0;
		return Int32.CopySign(1, BitConverter.SingleToInt32Bits(x));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int BitOrShiftSignFloat(float x)
	{
		if (x == 0.0f) return 0;
		var bits = BitConverter.SingleToInt32Bits(x);
		// positive: bits>>31=0     → 1|0=1
		// negative: bits>>31=-1    → 1|-1=-1  (arithmetic shift fills with sign bits)
		return 1 | (bits >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int UnsafeBitOrShiftSignFloat(float x)
	{
		if (x == 0.0f) return 0;
		var bits = Unsafe.As<float, int>(ref x); // x is a local parameter copy — safe
		return 1 | (bits >> 31);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int BranchlessSignFloat(float x)
	{
		var bits    = BitConverter.SingleToInt32Bits(x);
		var nonZero = ((bits & 0x7FFF_FFFF) != 0) ? 1 : 0; // 0 for ±0, 1 otherwise
		return (1 | (bits >> 31)) * nonZero;
	}

	// Double ────────────────────────────────────────────────────────────────

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int OldFastSignDouble(double x)
	{
		if (x == 0.0) return 0;
		return (int)Double.CopySign(1.0, x);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int BitOrShiftSignDouble(double x)
	{
		if (x == 0.0) return 0;
		var bits = BitConverter.DoubleToInt64Bits(x);
		// positive: bits>>63=0L        → 1|(int)0=1
		// negative: bits>>63=-1L (all ones) → 1|(int)-1=-1
		return 1 | (int)(bits >> 63);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int UnsafeBitOrShiftSignDouble(double x)
	{
		if (x == 0.0) return 0;
		var bits = Unsafe.As<double, long>(ref x); // x is a local parameter copy — safe
		return 1 | (int)(bits >> 63);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int BranchlessSignDouble(double x)
	{
		var bits    = BitConverter.DoubleToInt64Bits(x);
		var nonZero = ((bits & long.MaxValue) != 0) ? 1 : 0; // 0 for ±0, 1 otherwise
		return (1 | (int)(bits >> 63)) * nonZero;
	}

	// Generic INumber ─────────────────────────────────────────────────────────

	/// <summary>
	/// Generic INumber&lt;T&gt; implementation: T.IsZero + T.CopySign + Int32.CreateChecked.
	/// Works for any T : INumber&lt;T&gt; (int, float, double, …).
	///
	/// Measured results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
	///   float  → 0.760 ns (+43 % vs Math.Sign, 2.6× slower than BitOrShift)
	///   double → 0.758 ns (+45 % vs Math.Sign, 2.6× slower than BitOrShift)
	///   int    → 0.479 ns (+69 % vs Math.Sign)
	///
	/// Despite JIT devirtualisation of generic math in Release mode, the generic path
	/// carries the cost of three interface dispatches per call:
	///   T.IsZero     – FP zero-check
	///   T.CopySign   – FP intrinsic dispatch (hardware instruction, but extra call frame)
	///   CreateChecked – checked FP→int conversion with potential overflow path
	/// The type-specific BitOrShift path eliminates all FP arithmetic after the initial
	/// zero branch and is significantly faster.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GenericSign<T>(T value) where T : INumber<T>
	{
		if (T.IsZero(value))
			return 0;

		return Int32.CreateChecked(T.CopySign(T.One, value));
	}
}




