using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares MathF.Log10 / Math.Log10 (built-in) against three scalar FastLog10 candidates.
///
/// Candidates:
///   FastLog10    – bit-extraction + degree-4 Horner polynomial for ln(m), m ∈ [1, 2),
///                  then multiplied by log10(e) to convert ln → log10.
///                  log10(x) = e·log10(2) + ln(m)·log10(e)
///                  Key FP ops: 4 FMAs + 2 MULs + 1 bit-cast.
///
///   FastLog10V2  – direct base-10 polynomial with coefficients pre-multiplied by log10(e).
///                  Eliminates the final lnm * LOG10_E multiplication entirely.
///                  d_i = c_i * log10(e) are folded into the Horner coefficients at compile time.
///                  log10(x) = e·log10(2) + p_log10(m)
///                  Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast — saves 1 MUL vs FastLog10.
///
///   FastLog10V3  – via Math.Log2(x) · log10(2).
///                  Delegates to the built-in log2 intrinsic then scales.
///                  Expected similar throughput to the baseline (still a transcendental call).
///
/// Polynomial constants (degree-4 ln-approximation for m ∈ [1, 2)):
///   c0 = -1.741793927   c1 =  2.821202636   c2 = -1.469956800
///   c3 =  0.447178975   c4 = -0.056570851
///   Max relative error ≈ 8.7e-5 (fast-math trade-off).
///
/// V2 direct log10 polynomial (d_i = c_i * log10(e)):
///   d0 = -0.756451491   d1 =  1.225232737   d2 = -0.638394127
///   d3 =  0.194207361   d4 = -0.024568408
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method               Category  Mean      Ratio    Note
///   -------------------  --------  --------  ------   ------------------------------------------
///   DotNetLog10_Double   Double    2.020 ns  1.00x    built-in, IEEE-accurate
///   FastLog10_Double     Double    0.943 ns  0.47x    bit-extract + 4 FMAs + 2 MULs
///   FastLog10V2_Double   Double    0.892 ns  0.44x  ← new: folded coefficients, 1 MUL saved
///   FastLog10V3_Double   Double    2.022 ns  1.00x    via Math.Log2 — same as baseline
///
///   DotNetLog10_Float    Float     1.782 ns  1.00x    built-in
///   FastLog10_Float      Float     0.950 ns  0.53x    bit-extract + 4 FMAs + 2 MULs
///   FastLog10V2_Float    Float     0.897 ns  0.50x  ← new: folded coefficients, 1 MUL saved
///   FastLog10V3_Float    Float     1.511 ns  0.85x    via MathF.Log2 — hw transcendental, slower
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*Log10Benchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Log10Benchmark
{
	// 1 024 normal positive values spanning a wide exponent range.
	// Float: 10^x for x ∈ [-20, 20]  → values in [1e-20, 1e20], all normal floats.
	// Double: 10^x for x ∈ [-100, 100] → values in [1e-100, 1e100], all normal doubles.
	// Fixed seed; instance fields force the JIT to emit real loads per iteration.
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
			_floatData[i]  = (float)Math.Pow(10.0, rng.NextDouble() * 40.0 - 20.0);
			_doubleData[i] = Math.Pow(10.0, rng.NextDouble() * 200.0 - 100.0);
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in MathF.Log10 — hardware-accurate float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetLog10_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Log10(v);
		return sum;
	}

	/// <summary>
	/// FastLog10(float) — current ConstExpr optimizer output.
	/// Bit-extracts exponent e and mantissa m ∈ [1, 2), evaluates degree-4 Horner
	/// polynomial for ln(m), then converts: log10(x) = e·log10(2) + ln(m)·log10(e).
	/// Key FP ops: 4 FMAs + 2 MULs + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLog10_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastLog10Float(v);
		return sum;
	}

	/// <summary>
	/// FastLog10V2(float) — direct base-10 polynomial (coefficients = c_i * log10(e)).
	/// Folds the log10(e) conversion factor into the Horner coefficients at compile time,
	/// eliminating the final lnm * LOG10_E multiplication.
	/// Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast — saves 1 MUL vs FastLog10.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLog10V2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastLog10V2Float(v);
		return sum;
	}

	/// <summary>
	/// FastLog10V3(float) — via MathF.Log2(x) * log10(2).
	/// Delegates to the built-in log2 intrinsic then scales by log10(2).
	/// Expected similar throughput to the baseline (still a hardware transcendental call).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastLog10V3_Float()
	{
		var sum = 0f;
		const float LOG10_2 = 0.30102999566398120f;
		foreach (var v in _floatData)
			sum += MathF.Log2(v) * LOG10_2;
		return sum;
	}

	// ── double benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in Math.Log10 — hardware-accurate double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetLog10_Double()
	{
		var sum = 0d;
		foreach (var v in _doubleData)
			sum += Math.Log10(v);
		return sum;
	}

	/// <summary>
	/// FastLog10(double) — current ConstExpr optimizer output.
	/// Bit-extracts exponent e and mantissa m ∈ [1, 2), evaluates degree-4 Horner
	/// polynomial for ln(m), then converts: log10(x) = e·log10(2) + ln(m)·log10(e).
	/// Key FP ops: 4 FMAs + 2 MULs + 1 bit-cast.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLog10_Double()
	{
		var sum = 0d;
		foreach (var v in _doubleData)
			sum += FastLog10Double(v);
		return sum;
	}

	/// <summary>
	/// FastLog10V2(double) — direct base-10 polynomial (coefficients = c_i * log10(e)).
	/// Folds the log10(e) conversion factor into the Horner coefficients at compile time,
	/// eliminating the final lnm * LOG10_E multiplication.
	/// Key FP ops: 4 FMAs + 1 MUL + 1 bit-cast — saves 1 MUL vs FastLog10.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLog10V2_Double()
	{
		var sum = 0d;
		foreach (var v in _doubleData)
			sum += FastLog10V2Double(v);
		return sum;
	}

	/// <summary>
	/// FastLog10V3(double) — via Math.Log2(x) * log10(2).
	/// Delegates to the built-in log2 intrinsic then scales by log10(2).
	/// Expected similar throughput to the baseline (still a hardware transcendental call).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastLog10V3_Double()
	{
		var sum = 0d;
		const double LOG10_2 = 0.30102999566398119521373889472449303;
		foreach (var v in _doubleData)
			sum += Math.Log2(v) * LOG10_2;
		return sum;
	}

	// ── private implementations ──────────────────────────────────────────

	private static float FastLog10Float(float x)
	{
		if (Single.IsNaN(x) || x < 0f) return Single.NaN;
		if (x == 0f) return Single.NegativeInfinity;
		if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

		var bits = BitConverter.SingleToInt32Bits(x);
		var e    = (bits >> 23) - 127;
		var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

		// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const float c4 = -0.056570851f;
		const float c3 =  0.447178975f;
		const float c2 = -1.469956800f;
		const float c1 =  2.821202636f;
		const float c0 = -1.741793927f;

		var lnm = Single.FusedMultiplyAdd(c4, m, c3);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c0);

		const float LOG10_2 = 0.30102999566398120f;  // log10(2)
		const float LOG10_E = 0.43429448190325182f;  // log10(e) = 1/ln(10)

		return e * LOG10_2 + lnm * LOG10_E;
	}

	private static float FastLog10V2Float(float x)
	{
		if (Single.IsNaN(x) || x < 0f) return Single.NaN;
		if (x == 0f) return Single.NegativeInfinity;
		if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

		var bits = BitConverter.SingleToInt32Bits(x);
		var e    = (bits >> 23) - 127;
		var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

		// Direct log10 polynomial: d_i = c_i * log10(e), folded at compile time.
		// Eliminates the final lnm * LOG10_E multiplication vs FastLog10.
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const float d4 = -0.024568408f;  // c4 * log10(e)
		const float d3 =  0.194207361f;  // c3 * log10(e)
		const float d2 = -0.638394127f;  // c2 * log10(e)
		const float d1 =  1.225232737f;  // c1 * log10(e)
		const float d0 = -0.756451491f;  // c0 * log10(e)

		var log10m = Single.FusedMultiplyAdd(d4, m, d3);
		log10m     = Single.FusedMultiplyAdd(log10m, m, d2);
		log10m     = Single.FusedMultiplyAdd(log10m, m, d1);
		log10m     = Single.FusedMultiplyAdd(log10m, m, d0);

		const float LOG10_2 = 0.30102999566398120f;

		return e * LOG10_2 + log10m;
	}

	private static double FastLog10Double(double x)
	{
		if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
		if (x == 0.0) return Double.NegativeInfinity;
		if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

		var bits = BitConverter.DoubleToInt64Bits(x);
		var e    = (int)((bits >> 52) - 1023L);
		var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

		// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const double c4 = -0.056570851;
		const double c3 =  0.447178975;
		const double c2 = -1.469956800;
		const double c1 =  2.821202636;
		const double c0 = -1.741793927;

		var lnm = Math.FusedMultiplyAdd(c4, m, c3);
		lnm     = Math.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Math.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Math.FusedMultiplyAdd(lnm, m, c0);

		const double LOG10_2 = 0.30102999566398119521373889472449303;
		const double LOG10_E = 0.43429448190325182765112891891660508;

		return e * LOG10_2 + lnm * LOG10_E;
	}

	private static double FastLog10V2Double(double x)
	{
		if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
		if (x == 0.0) return Double.NegativeInfinity;
		if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

		var bits = BitConverter.DoubleToInt64Bits(x);
		var e    = (int)((bits >> 52) - 1023L);
		var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

		// Direct log10 polynomial: d_i = c_i * log10(e), folded at compile time.
		// Eliminates the final lnm * LOG10_E multiplication vs FastLog10.
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		const double d4 = -0.024568408426;  // c4 * log10(e)
		const double d3 =  0.194207361266;  // c3 * log10(e)
		const double d2 = -0.638394126876;  // c2 * log10(e)
		const double d1 =  1.225232737146;  // c1 * log10(e)
		const double d0 = -0.756451491109;  // c0 * log10(e)

		var log10m = Math.FusedMultiplyAdd(d4, m, d3);
		log10m     = Math.FusedMultiplyAdd(log10m, m, d2);
		log10m     = Math.FusedMultiplyAdd(log10m, m, d1);
		log10m     = Math.FusedMultiplyAdd(log10m, m, d0);

		const double LOG10_2 = 0.30102999566398119521373889472449303;

		return e * LOG10_2 + log10m;
	}
}










