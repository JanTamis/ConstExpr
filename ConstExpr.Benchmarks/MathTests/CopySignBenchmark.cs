using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar CopySign implementations for float and double.
///
/// Three groups are benchmarked independently:
///   Float  – MathF.CopySign  vs BitConverter bits  vs Unsafe.As bits  vs Ternary/Abs
///   Double – Math.CopySign   vs BitConverter bits  vs Unsafe.As bits  vs Ternary/Abs
///
/// Current optimizer output:
///   The CopySignFunctionOptimizer forwards the general case to T.CopySign(x, y),
///   which maps to Math.CopySign / MathF.CopySign (hardware intrinsic on .NET 10).
///
/// Alternative implementations evaluated here:
///   BitConverter – reinterpret float/double as int/long via BitConverter,
///     mask the magnitude bits from x and the sign bit from y, reinterpret back.
///     Semantically equivalent and allocation-free, but carries two round-trip calls.
///   Unsafe.As   – same bit-manipulation but uses Unsafe.As&lt;T,U&gt; to avoid any helper
///     indirection; lets the JIT see the pattern inline and potentially fold to a single
///     AND/ORR pair (same instruction count as the intrinsic on ARM64).
///   Ternary     – Math.Abs(x) then branch on sign(y). Clean, readable, but adds a
///     conditional instruction after FABS; may be slower than branchless alternatives.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*CopySignBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CopySignBenchmark
{
	private const int N = 1_024;

	private float[]  _floatX  = null!;
	private float[]  _floatY  = null!;
	private double[] _doubleX = null!;
	private double[] _doubleY = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatX  = new float[N];
		_floatY  = new float[N];
		_doubleX = new double[N];
		_doubleY = new double[N];

		for (var i = 0; i < N; i++)
		{
			var x = rng.NextDouble() * 200.0 - 100.0; // [-100, 100], mixed signs
			var y = rng.NextDouble() * 200.0 - 100.0;
			_floatX[i]  = (float)x;
			_floatY[i]  = (float)y;
			_doubleX[i] = x;
			_doubleY[i] = y;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.CopySign — hardware intrinsic, full-precision.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetCopySign_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += MathF.CopySign(_floatX[i], _floatY[i]);
		return sum;
	}

	/// <summary>
	/// Bit-manipulation via BitConverter.SingleToInt32Bits / Int32BitsToSingle.
	/// Masks magnitude bits of x and sign bit of y, reassembles.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float BitConverterCopySign_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += CopySignBitConverterFloat(_floatX[i], _floatY[i]);
		return sum;
	}

	/// <summary>
	/// Bit-manipulation via Unsafe.As — same mask logic but bypasses BitConverter
	/// helpers; gives the JIT maximum visibility to fold to AND/ORR pair.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float UnsafeCopySign_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += CopySignUnsafeFloat(_floatX[i], _floatY[i]);
		return sum;
	}

	/// <summary>
	/// Ternary: MathF.Abs(x) then -absX if y &lt; 0 else absX.
	/// Branchless FABS + FCSEL on ARM64 but one extra instruction vs intrinsic.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float TernaryCopySign_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += CopySignTernaryFloat(_floatX[i], _floatY[i]);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.CopySign — hardware intrinsic, full-precision.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetCopySign_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += Math.CopySign(_doubleX[i], _doubleY[i]);
		return sum;
	}

	/// <summary>
	/// Bit-manipulation via BitConverter.DoubleToInt64Bits / Int64BitsToDouble.
	/// Uses long.MaxValue / long.MinValue as the magnitude / sign masks.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double BitConverterCopySign_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += CopySignBitConverterDouble(_doubleX[i], _doubleY[i]);
		return sum;
	}

	/// <summary>
	/// Bit-manipulation via Unsafe.As — same logic but zero helper overhead;
	/// ideal JIT output is a pair of AND/ORR instructions, identical to the
	/// intrinsic expansion on x64 (VANDPD/VORPD).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double UnsafeCopySign_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += CopySignUnsafeDouble(_doubleX[i], _doubleY[i]);
		return sum;
	}

	/// <summary>
	/// Ternary: Math.Abs(x) then -absX if y &lt; 0 else absX.
	/// One branch after FABS; may mispredict on alternating sign data.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double TernaryCopySign_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += CopySignTernaryDouble(_doubleX[i], _doubleY[i]);
		return sum;
	}

	// ── scalar implementations ─────────────────────────────────────────────

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CopySignBitConverterFloat(float x, float y)
	{
		var xBits = BitConverter.SingleToInt32Bits(x);
		var yBits = BitConverter.SingleToInt32Bits(y);
		return BitConverter.Int32BitsToSingle((xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000)));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CopySignUnsafeFloat(float x, float y)
	{
		var xBits = Unsafe.As<float, int>(ref x);
		var yBits = Unsafe.As<float, int>(ref y);
		var result = (xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000));
		return Unsafe.As<int, float>(ref result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CopySignTernaryFloat(float x, float y)
	{
		var absX = MathF.Abs(x);
		return y < 0f ? -absX : absX;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CopySignBitConverterDouble(double x, double y)
	{
		var xBits = BitConverter.DoubleToInt64Bits(x);
		var yBits = BitConverter.DoubleToInt64Bits(y);
		return BitConverter.Int64BitsToDouble((xBits & long.MaxValue) | (yBits & long.MinValue));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CopySignUnsafeDouble(double x, double y)
	{
		var xBits = Unsafe.As<double, long>(ref x);
		var yBits = Unsafe.As<double, long>(ref y);
		var result = (xBits & long.MaxValue) | (yBits & long.MinValue);
		return Unsafe.As<long, double>(ref result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CopySignTernaryDouble(double x, double y)
	{
		var absX = Math.Abs(x);
		return y < 0.0 ? -absX : absX;
	}
}


