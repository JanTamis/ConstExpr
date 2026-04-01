using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar RootN implementations for float and double.
///
/// Five candidates are benchmarked per type:
///   DotNet       – float.RootN / double.RootN  (runtime / hardware implementation)
///   Current      – FastRootN from RootNFunctionOptimizer: bit-hack + 3× Newton, O(n) inner loop
///   V2           – Optimized: bit-hack + precomputed reciprocal 1/n + O(log n) binary exponentiation
///                  for y^(n-1) + 3× Newton steps unrolled (no outer loop).
///   ExpLog       – MathF.Exp(MathF.Log(x) / n) / Math.Exp(Math.Log(x) / n)
///                  Purely hardware: 1 LOG + 1 DIV + 1 EXP. Constant cost for any n.
///   FastExpLog   – FastExp(FastLog(x) / n) using polynomial approximations from
///                  LogFunctionOptimizer (degree-4 Horner) + ExpFunctionOptimizer
///                  (degree-3 float / degree-4 double Horner).
///                  Avoids hardware LOG/EXP instructions; faster on CPUs where those are slow.
///
/// All benchmarks parameterized over Root ∈ { 5, 7, 10 }.
/// n=2 (Sqrt) and n=3 (Cbrt) are already special-cased in the optimizer, so excluded here.
///
/// Optimization insight — Current vs V2:
///   Newton step requires y^(n-1). Current computes it with (n-1) multiplications in a loop.
///   V2 replaces this with O(log n) binary exponentiation — saving:
///     n=5:  1 mult/step × 3 = 3  fewer muls per call
///     n=7:  2 mults/step × 3 = 6  fewer muls per call
///    n=10:  5 mults/step × 3 = 15 fewer muls per call
///   V2 also precomputes 1/n (1 int divide → 3 float multiplies instead of 3 int divides)
///   and unrolls both the outer Newton loop and inner power loop.
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Method             n=5        n=7        n=10       Note
///   ----------------   --------   --------   --------   ------------------------------------------------
///   DotNet_Double      5.566 ns   5.571 ns   5.574 ns   double.RootN — baseline
///   Current_Double     7.119 ns   6.441 ns   10.388 ns  bit-hack + O(n) Newton — degrades with n!
///   V2_Double          4.982 ns   4.956 ns   6.267 ns   bit-hack + O(log n) Newton
///   ExpLog_Double      4.851 ns   4.750 ns   4.743 ns   hardware LOG + DIV + EXP
///   FastExpLog_Double  2.311 ns   2.295 ns   2.296 ns ← FASTEST — polynomial FastLog + FastExp
///
///   DotNet_Float       5.987 ns   5.878 ns   5.879 ns   float.RootN — baseline
///   Current_Float      4.236 ns   5.908 ns   8.933 ns   bit-hack + O(n) Newton — degrades with n!
///   V2_Float           4.950 ns   4.799 ns   6.112 ns   bit-hack + O(log n) Newton
///   ExpLog_Float       3.005 ns   2.992 ns   2.994 ns   hardware LOG + DIV + EXP
///   FastExpLog_Float   2.261 ns   2.220 ns   2.220 ns ← FASTEST — polynomial FastLog + FastExp
///
/// Key findings:
///   1. FastExpLog wins for both float and double across all n values:
///      Float:  ~2.24 ns constant — 1.34× faster than hardware ExpLog, 2.6× faster than DotNet.
///      Double: ~2.30 ns constant — 2.06× faster than hardware ExpLog, 2.4× faster than DotNet.
///   2. Current degrades sharply with n: O(n) loop makes it 4.2 ns (n=5) → 8.9 ns (n=10).
///   3. V2 (O(log n) fast pow) eliminates degradation but still slower than ExpLog.
///   4. RootNFunctionOptimizer now emits FastExp(FastLog(x) / n) for both float and double.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*RootNBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class RootNBenchmark
{
	private const int N = 1_024;
	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	[Params(5, 7, 10)]
	public int Root { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData  = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			// Positive values spread across many magnitudes (1e-4 … 1e6).
			// Avoids NaN/negative-base branches so we measure the hot path only.
			var exp = rng.NextDouble() * 10.0 - 4.0;
			var v   = Math.Pow(10.0, exp);
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────────

	/// <summary>Built-in float.RootN — runtime/hardware implementation, baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNet_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.RootN(v, Root);
		return sum;
	}

	/// <summary>
	/// Current FastRootN(float) from RootNFunctionOptimizer.
	/// Bit-hack initial estimate + 3× Newton, O(n) inner loop for y^(n-1),
	/// plus an integer divide by n per Newton step.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float Current_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastRootNFloat(v, Root);
		return sum;
	}

	/// <summary>
	/// FastRootNV2(float) — optimized replacement.
	/// Bit-hack + precomputed 1/n + O(log n) binary exponentiation for y^(n-1)
	/// + 3× Newton steps unrolled (no outer loop, no inner loop).
	/// Net savings vs Current for n=10: ~15 fewer multiplications per call.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float V2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastRootNV2Float(v, Root);
		return sum;
	}

	/// <summary>
	/// ExpLog(float): MathF.Exp(MathF.Log(x) / n) — 1 LOG + 1 DIV + 1 EXP, constant cost for any n.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float ExpLog_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Exp(MathF.Log(v) / Root);
		return sum;
	}

	/// <summary>
	/// FastExpLog(float): FastExp(FastLog(x) / n) — polynomial approximations from
	/// LogFunctionOptimizer (degree-4 Horner for ln) + ExpFunctionOptimizer (degree-3 Horner for 2^r).
	/// Avoids hardware LOG/EXP; potentially faster on CPUs where those transcendentals are slow.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastExpLog_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastExpLogFloat(v, Root);
		return sum;
	}

	// ── double ──────────────────────────────────────────────────────────────────

	/// <summary>Built-in double.RootN — runtime/hardware implementation, baseline.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNet_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.RootN(v, Root);
		return sum;
	}

	/// <summary>
	/// Current FastRootN(double) from RootNFunctionOptimizer.
	/// Bit-hack + 3× Newton, O(n) inner loop + integer divide per step.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double Current_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastRootNDouble(v, Root);
		return sum;
	}

	/// <summary>
	/// FastRootNV2(double) — optimized replacement.
	/// Bit-hack + precomputed 1/n + O(log n) fast power + 3× Newton unrolled.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double V2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastRootNV2Double(v, Root);
		return sum;
	}

	/// <summary>
	/// ExpLog(double): Math.Exp(Math.Log(x) / n) — 1 LOG + 1 DIV + 1 EXP, constant cost for any n.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double ExpLog_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Exp(Math.Log(v) / Root);
		return sum;
	}

	/// <summary>
	/// FastExpLog(double): FastExp(FastLog(x) / n) — polynomial approximations from
	/// LogFunctionOptimizer (degree-4 Horner for ln) + ExpFunctionOptimizer (degree-4 Horner for 2^r).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastExpLog_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastExpLogDouble(v, Root);
		return sum;
	}

	// ── float implementations ────────────────────────────────────────────────────

	/// <summary>
	/// Exact copy of the float FastRootN body emitted by the current optimizer.
	/// O(n) inner loop: computes y^(n-1) with (n-1) multiplications per Newton step.
	/// </summary>
	private static float CurrentFastRootNFloat(float x, int n)
	{
		if (n == 0) return float.NaN;
		if (n == 1) return x;
		if (x == 0.0f) return 0.0f;
		if (n < 0) return 1.0f / CurrentFastRootNFloat(x, -n);

		var absX = MathF.Abs(x);

		var i = BitConverter.SingleToInt32Bits(absX);
		i = 0x3f800000 + (i - 0x3f800000) / n;
		var y = BitConverter.Int32BitsToSingle(i);

		var nMinus1 = n - 1;
		for (var iter = 0; iter < 3; iter++)
		{
			var yPow = 1.0f;
			for (var j = 0; j < nMinus1; j++)
				yPow *= y;
			y = (nMinus1 * y + absX / yPow) / n;
		}

		if (x < 0.0f && (n & 1) != 0)
			return -y;
		return y;
	}

	/// <summary>
	/// Optimized FastRootN(float): O(log n) binary exponentiation replaces O(n) loop;
	/// 1/n precomputed to replace integer divisions with float multiplications; Newton unrolled.
	/// </summary>
	private static float FastRootNV2Float(float x, int n)
	{
		if (n == 0) return float.NaN;
		if (n == 1) return x;
		if (x == 0.0f) return 0.0f;
		if (n < 0) return 1.0f / FastRootNV2Float(x, -n);

		var absX    = MathF.Abs(x);
		var nMinus1 = n - 1;
		var recN    = 1.0f / n;   // precomputed reciprocal: 1 int divide, 3 float muls below

		// Bit-hack initial approximation: divide exponent field by n
		var bits = BitConverter.SingleToInt32Bits(absX);
		bits = 0x3f800000 + (bits - 0x3f800000) / n;
		var y = BitConverter.Int32BitsToSingle(bits);

		// 3× Newton-Raphson, unrolled. y^(n-1) via O(log n) binary exponentiation.
		var yPow = IntPowFloat(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		yPow = IntPowFloat(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		yPow = IntPowFloat(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		if (x < 0.0f && (n & 1) != 0)
			return -y;
		return y;
	}

	/// <summary>O(log n) integer power for float via binary (square-and-multiply) exponentiation.</summary>
	private static float IntPowFloat(float y, int n)
	{
		var result = 1.0f;
		while (n > 0)
		{
			if ((n & 1) != 0) result *= y;
			y *= y;
			n >>= 1;
		}
		return result;
	}

	// ── double implementations ───────────────────────────────────────────────────

	/// <summary>
	/// Exact copy of the double FastRootN body emitted by the current optimizer.
	/// O(n) inner loop per Newton step.
	/// </summary>
	private static double CurrentFastRootNDouble(double x, int n)
	{
		if (n == 0) return double.NaN;
		if (n == 1) return x;
		if (x == 0.0) return 0.0;
		if (n < 0) return 1.0 / CurrentFastRootNDouble(x, -n);

		var absX = Math.Abs(x);

		var i = BitConverter.DoubleToInt64Bits(absX);
		i = 0x3ff0000000000000L + (i - 0x3ff0000000000000L) / n;
		var y = BitConverter.Int64BitsToDouble(i);

		var nMinus1 = n - 1;
		for (var iter = 0; iter < 3; iter++)
		{
			var yPow = 1.0;
			for (var j = 0; j < nMinus1; j++)
				yPow *= y;
			y = (nMinus1 * y + absX / yPow) / n;
		}

		if (x < 0.0 && (n & 1) != 0)
			return -y;
		return y;
	}

	/// <summary>
	/// Optimized FastRootN(double): O(log n) binary exponentiation + precomputed 1/n + Newton unrolled.
	/// </summary>
	private static double FastRootNV2Double(double x, int n)
	{
		if (n == 0) return double.NaN;
		if (n == 1) return x;
		if (x == 0.0) return 0.0;
		if (n < 0) return 1.0 / FastRootNV2Double(x, -n);

		var absX    = Math.Abs(x);
		var nMinus1 = n - 1;
		var recN    = 1.0 / n;

		var bits = BitConverter.DoubleToInt64Bits(absX);
		bits = 0x3ff0000000000000L + (bits - 0x3ff0000000000000L) / n;
		var y = BitConverter.Int64BitsToDouble(bits);

		var yPow = IntPowDouble(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		yPow = IntPowDouble(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		yPow = IntPowDouble(y, nMinus1);
		y = (nMinus1 * y + absX / yPow) * recN;

		if (x < 0.0 && (n & 1) != 0)
			return -y;
		return y;
	}

	/// <summary>O(log n) integer power for double via binary (square-and-multiply) exponentiation.</summary>
	private static double IntPowDouble(double y, int n)
	{
		var result = 1.0;
		while (n > 0)
		{
			if ((n & 1) != 0) result *= y;
			y *= y;
			n >>= 1;
		}
		return result;
	}

	// ── FastExpLog implementations (polynomial FastLog + FastExp) ────────────────

	/// <summary>
	/// FastExpLog(float): FastExp(FastLog(x) / n).
	/// Combines the polynomial log and exp from LogFunctionOptimizer / ExpFunctionOptimizer.
	/// </summary>
	private static float FastExpLogFloat(float x, int n)
	{
		if (n == 0) return float.NaN;
		if (n == 1) return x;
		if (x == 0.0f) return 0.0f;
		if (n < 0) return 1.0f / FastExpLogFloat(x, -n);
		if (x < 0.0f && (n & 1) != 0)
			return -FastExpFloat(-FastLogFloat(-x) / n);
		return FastExpFloat(FastLogFloat(x) / n);
	}

	/// <summary>
	/// FastExpLog(double): FastExp(FastLog(x) / n).
	/// </summary>
	private static double FastExpLogDouble(double x, int n)
	{
		if (n == 0) return double.NaN;
		if (n == 1) return x;
		if (x == 0.0) return 0.0;
		if (n < 0) return 1.0 / FastExpLogDouble(x, -n);
		if (x < 0.0 && (n & 1) != 0)
			return -FastExpDouble(-FastLogDouble(-x) / n);
		return FastExpDouble(FastLogDouble(x) / n);
	}

	// ── FastLog — exact copy of LogFunctionOptimizer output ─────────────────────

	/// <summary>
	/// FastLog(float): degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
	/// From LogFunctionOptimizer. Max relative error ≈ 8.7e-5.
	/// Benchmarked ~2× faster than MathF.Log on ARM64.
	/// </summary>
	private static float FastLogFloat(float x)
	{
		if (float.IsNaN(x) || x < 0f) return float.NaN;
		if (x == 0f) return float.NegativeInfinity;
		if (float.IsPositiveInfinity(x)) return float.PositiveInfinity;

		var bits = BitConverter.SingleToInt32Bits(x);
		var e    = (bits >> 23) - 127;
		var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

		const float c4 = -0.056570851f;
		const float c3 =  0.447178975f;
		const float c2 = -1.469956800f;
		const float c1 =  2.821202636f;
		const float c0 = -1.741793927f;

		var lnm = Single.FusedMultiplyAdd(c4, m, c3);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Single.FusedMultiplyAdd(lnm, m, c0);

		const float LN2 = 0.6931471805599453f;
		return e * LN2 + lnm;
	}

	/// <summary>
	/// FastLog(double): degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
	/// From LogFunctionOptimizer. Max relative error ≈ 8.7e-5.
	/// Benchmarked ~2.2× faster than Math.Log on ARM64.
	/// </summary>
	private static double FastLogDouble(double x)
	{
		if (double.IsNaN(x) || x < 0.0) return double.NaN;
		if (x == 0.0) return double.NegativeInfinity;
		if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;

		var bits = BitConverter.DoubleToInt64Bits(x);
		var e    = (int)((bits >> 52) - 1023L);
		var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

		const double c4 = -0.056570851;
		const double c3 =  0.447178975;
		const double c2 = -1.469956800;
		const double c1 =  2.821202636;
		const double c0 = -1.741793927;

		var lnm = Double.FusedMultiplyAdd(c4, m, c3);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c2);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c1);
		lnm     = Double.FusedMultiplyAdd(lnm, m, c0);

		const double LN2 = 0.6931471805599453094172321214581766;
		return e * LN2 + lnm;
	}

	// ── FastExp — exact copy of ExpFunctionOptimizer output ─────────────────────

	/// <summary>
	/// FastExp(float): range reduction to r ∈ [-0.5, 0.5] + degree-3 Horner for 2^r.
	/// From ExpFunctionOptimizer. Hot path: 1 MUL + FRINTN + FCVTZS + 3 FMA + bit-reconstruct.
	/// </summary>
	private static float FastExpFloat(float x)
	{
		if (x >= 88.0f) return float.PositiveInfinity;
		if (x <= -87.0f) return 0.0f;

		const float INV_LN2 = 1.4426950408889634f;

		var kf = x * INV_LN2;
		var k  = (int)Single.Round(kf);
		var r  = kf - k;

		const float c3 = 0.055504108664821580f;
		const float c2 = 0.240226506959100690f;
		const float c1 = 0.693147180559945309f;

		var p    = Single.FusedMultiplyAdd(c3, r, c2);
		p        = Single.FusedMultiplyAdd(p,  r, c1);
		var expR = Single.FusedMultiplyAdd(p,  r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	/// <summary>
	/// FastExp(double): range reduction to r ∈ [-0.5, 0.5] + degree-4 Horner for 2^r.
	/// From ExpFunctionOptimizer. Hot path: 1 MUL + FRINTN + FCVTZS + 4 FMA + bit-reconstruct.
	/// </summary>
	private static double FastExpDouble(double x)
	{
		if (x >= 709.0) return double.PositiveInfinity;
		if (x <= -708.0) return 0.0;

		const double INV_LN2 = 1.4426950408889634073599246810018921;

		var kf = x * INV_LN2;
		var k  = (long)Double.Round(kf);
		var r  = kf - k;

		const double c4 = 9.618129107628477232e-3;
		const double c3 = 5.550410866482157995e-2;
		const double c2 = 2.402265069591006909e-1;
		const double c1 = 6.931471805599453094e-1;

		var p    = Double.FusedMultiplyAdd(c4, r, c3);
		p        = Double.FusedMultiplyAdd(p,  r, c2);
		p        = Double.FusedMultiplyAdd(p,  r, c1);
		var expR = Double.FusedMultiplyAdd(p,  r, 1.0);

		var bits = (ulong)((k + 1023L) << 52);
		return BitConverter.UInt64BitsToDouble(bits) * expR;
	}
}








