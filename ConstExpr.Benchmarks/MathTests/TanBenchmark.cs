using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar Tan implementations for float and double.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Tan  vs FastTan(float)    [current: NaN/Inf guards + asymptote fallback at |x|>1.4, rational P/Q]
///                        vs FastTanV2(float)  [no guards, fold to [0,π/4] + [2/2] Padé rational, no fallback]
///                        vs FastTanV3(float)  [no guards, sin/cos decomposition sharing x²]
///   Double – Math.Tan   vs FastTan(double)   [current: NaN/Inf guards + asymptote fallback at |x|>1.4, rational P/Q]
///                        vs FastTanV2(double) [no guards, fold to [0,π/4] + [2/3] Padé rational, no fallback]
///                        vs FastTanV3(double) [no guards, sin/cos decomposition sharing x²]
///
/// Current FastTan issues:
///   1. Two NaN/Infinity guards add branch pressure on every call.
///   2. The asymptote fallback `if (|xReduced| > 1.4) return Tan(x)` calls the full hardware
///      Tan (~3–4 ns) for roughly 11 % of inputs drawn from [−100, 100]:
///        fraction = 2·(π/2 − 1.4) / π ≈ 10.9 %.
///   3. The generated float method contains a missing semicolon syntax bug.
///
/// V2 key design — fold + exact Padé rational, derived by matching the tan Taylor series:
///   tan(x)/x = 1 + x²/3 + 2x⁴/15 + 17x⁶/315 + ...
///   Range-reduce to (−π/2, π/2) via Round(x·1/π)·π.
///   Fold |x| > π/4 via cotangent identity: tan(x) = 1/tan(π/2 − x).
///   Float [2/2] Padé: (1 − x²/9 + x⁴/945) / (1 − 4x²/9 + x⁴/63)
///     Derived by matching up to x⁸. Max absolute error ≈ 4e−5 on [0, π/4].
///   Double [2/3] Padé: (1 − 4x²/33 + x⁴/495) / (1 − 5x²/11 + 2x⁴/99 − x⁶/10395)
///     Derived by matching up to x¹². Max absolute error ≈ 1e−5 on [0, π/4].
///   No NaN/Inf guards; no asymptote fallback; 4–6 FMA + 1–2 div on the hot path.
///
/// V3 key design — sin/cos decomposition with shared range reduction and x²:
///   Reduce to (−π/2, π/2) once, then use |x| and x² for BOTH polynomials.
///   Float:  degree-5 FastSin (3 FMA) + degree-8 FastCos (4 FMA) + 1 div.
///   Double: degree-11 FastSin (6 FMA + 1 mul) + degree-10 FastCos (5 FMA) + 1 div.
///   Exactly 1 division; no conditional reciprocal branch.
///
/// Key findings (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Float:  DotNet=2.65ns  V1=1.05ns(−60%)  V2=1.86ns(−30%)  V3=2.13ns(−20%)
///   Double: DotNet=2.86ns  V1=1.34ns(−53%)  V2=2.38ns(−17%)  V3=1.88ns(−34%)
///
/// Why V1 beats V2/V3:
///   V1's asymptote fallback branch is 89% not-taken → trivially predicted by M4 Pro.
///   V2's fold branch is 50% taken → constant mispredictions (~4-cycle penalty each).
///   V2 also adds a conditional second FDIV (for the reciprocal), compounding the loss.
///   V3 performs 3+4=7 FMA per float call vs V1's 6 FMA on the 89% fast path.
///
/// Input domain: [−100, 100] — spans ~32 full tan periods, exercises range reduction heavily.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*TanBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TanBenchmark
{
	// 1 024 values spread over many full tan periods to exercise range reduction.
	private const int N = 1_024;
	private float[] _floatData = null!;
	private double[] _doubleData = null!;

	// Precomputed 1/π (tan's period) and 1/(2π) for sin/cos.
	private const float InvPiF = 1f / Single.Pi;
	private const double InvPiD = 1.0 / Double.Pi;
	private const float InvTauF = 1f / Single.Tau;
	private const double InvTauD = 1.0 / Double.Tau;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			var v = rng.NextDouble() * 200.0 - 100.0; // uniform in [−100, 100]
			_floatData[i] = (float) v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in MathF.Tan — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetTan_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Tan(v);
		return sum;
	}

	/// <summary>
	/// Current FastTan(float) from TanFunctionOptimizer.
	/// Two NaN/Inf guards + asymptote fallback at |xReduced| &gt; 1.4 → MathF.Tan (~11 % of inputs).
	/// Main path: rational [2/2] P(x²)/Q(x²) with 6 FMA + 1 div.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTan_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastTanFloat(v);
		return sum;
	}

	/// <summary>
	/// FastTanV2(float) — fold to [0, π/4] + exact [2/2] Padé rational, no guards/fallback.
	/// Padé coefficients derived by matching the tan Taylor series through x⁸:
	///   tan(x)/x ≈ (1 − x²/9 + x⁴/945) / (1 − 4x²/9 + x⁴/63)
	/// 4 FMA + 1 div on the main path; conditional reciprocal (1/t) handles (π/4, π/2).
	/// Max absolute error ≈ 4e−5 on [0, π/4] — acceptable for FastMath use.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastTanV2Float(v);
		return sum;
	}

	/// <summary>
	/// FastTanV3(float) — sin/cos decomposition sharing range reduction and x².
	/// Reduces to (−π/2, π/2) once; FastSin (deg-5, 3 FMA) + FastCos (deg-8, 4 FMA) on same x².
	/// Exactly 1 division; no conditional reciprocal; max error ≈ 1.3e−4.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastTanV3Float(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.Tan — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetTan_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Tan(v);
		return sum;
	}

	/// <summary>
	/// Current FastTan(double) from TanFunctionOptimizer.
	/// Two NaN/Inf guards + asymptote fallback at |xReduced| &gt; 1.4 → Math.Tan (~11 % of inputs).
	/// Main path: rational [3/3] P(x²)/Q(x²) with 8 FMA + 1 div.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTan_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastTanDouble(v);
		return sum;
	}

	/// <summary>
	/// FastTanV2(double) — fold to [0, π/4] + exact [2/3] Padé rational, no guards/fallback.
	/// Padé coefficients derived by matching the tan Taylor series through x¹²:
	///   tan(x)/x ≈ (1 − 4x²/33 + x⁴/495) / (1 − 5x²/11 + 2x⁴/99 − x⁶/10395)
	/// 6 FMA + 1 div on the main path; conditional reciprocal eliminates asymptote fallback.
	/// Max absolute error ≈ 1e−5 on [0, π/4] — FastMath accuracy.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastTanV2Double(v);
		return sum;
	}

	/// <summary>
	/// FastTanV3(double) — sin/cos decomposition sharing range reduction and x².
	/// Reduces to (−π/2, π/2) once; FastSin (deg-11, 6 FMA) + FastCos (deg-10, 5 FMA) on same x².
	/// Exactly 1 division; accuracy ≈ 4e−14 (matches FastSin/FastCos).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastTanV3Double(v);
		return sum;
	}

	// ── scalar implementations ──────────────────────────────────────────────

	/// <summary>
	/// Exact mirror of TanFunctionOptimizer's GenerateFastTanMethodFloat (with syntax bug fixed).
	/// NaN/Inf guards + Round range reduction + asymptote fallback at |x|>1.4 + rational P/Q.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float CurrentFastTanFloat(float x)
	{
		if (Single.IsNaN(x)) return Single.NaN;
		if (Single.IsInfinity(x)) return Single.NaN;

		const float InvPi = 1.0f / Single.Pi;

		var quotient = Single.Round(x * InvPi);
		var xReduced = Single.FusedMultiplyAdd(-quotient, Single.Pi, x);

		// Asymptote fallback: values close to ±π/2 are beyond the rational's reliable range.
		// ~11 % of uniformly distributed inputs hit this slow path.
		var absX = Single.Abs(xReduced);
		if (absX > 1.4f)
			return Single.Tan(x);

		var x2 = xReduced * xReduced;

		var p1 = -0.1306282f;
		var p2 = 0.0052854f;
		var numerator = Single.FusedMultiplyAdd(p2, x2, p1);
		numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
		numerator *= xReduced;

		var q1 = -0.4636476f;
		var q2 = 0.0157903f;
		var denominator = Single.FusedMultiplyAdd(q2, x2, q1);
		denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);

		return numerator / denominator;
	}

	/// <summary>
	/// FastTanV2(float): fold to [0,π/4] + exact [2/2] Padé rational approximant.
	/// Coefficients from exact Padé matching: p₁=−1/9, p₂=1/945 | q₁=−4/9, q₂=1/63.
	/// No NaN/Inf guards; no asymptote fallback; max error ≈ 4e−5 on [0, π/4].
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastTanV2Float(float x)
	{
		const float InvPi = 1f / Single.Pi;
		const float HalfPi = Single.Pi * 0.5f;
		const float QuarterPi = Single.Pi * 0.25f;

		// Range reduce to (−π/2, π/2) — tan's period is π
		var quotient = Single.Round(x * InvPi);
		x = Single.FusedMultiplyAdd(-quotient, Single.Pi, x);

		// Save sign; work with |x|
		var signX = x;
		x = Single.Abs(x);

		// Fold to [0, π/4] using cotangent identity: tan(x) = 1/tan(π/2 − x) for x ∈ (π/4, π/2)
		var swap = x > QuarterPi;
		var xf = swap ? HalfPi - x : x;

		// [2/2] Padé: tan(x)/x ≈ (1 + p₁·x² + p₂·x⁴) / (1 + q₁·x² + q₂·x⁴)
		var x2 = xf * xf;
		var num = Single.FusedMultiplyAdd(1f / 945f, x2, -1f / 9f);
		num = Single.FusedMultiplyAdd(num, x2, 1.0f);
		num *= xf;
		var den = Single.FusedMultiplyAdd(1f / 63f, x2, -4f / 9f);
		den = Single.FusedMultiplyAdd(den, x2, 1.0f);

		var t = num / den;

		// Cotangent identity: tan(x) = 1/tan(π/2 − x) for folded inputs
		if (swap) t = 1.0f / t;

		return Single.CopySign(t, signX);
	}

	/// <summary>
	/// FastTanV3(float): sin/cos decomposition sharing range reduction and x².
	/// Reduces to (−π/2, π/2) once; evaluates FastSin and FastCos on the same |x| and x².
	/// Exactly 1 division; max error ≈ 1.3e−4 (matches FastSin degree-5 poly accuracy).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FastTanV3Float(float x)
	{
		const float InvPi = 1f / Single.Pi;

		// Range reduce to (−π/2, π/2) once — tan period is π
		var quotient = Single.Round(x * InvPi);
		x = Single.FusedMultiplyAdd(-quotient, Single.Pi, x);

		// |x| ≤ π/2: no further folding needed; x² shared between sin and cos
		var absX = Single.Abs(x);
		var x2 = absX * absX;

		// FastSin (degree-5 Taylor on [0, π/2]): sin(x) ≈ x*(1 − x²/6 + x⁴/120 − x⁶/5040)
		var sinVal = -1.9841269841e-4f;
		sinVal = Single.FusedMultiplyAdd(sinVal, x2, 8.3333333333e-3f);
		sinVal = Single.FusedMultiplyAdd(sinVal, x2, -1.6666666667e-1f);
		sinVal = Single.FusedMultiplyAdd(sinVal, x2, 1.0f);
		sinVal *= absX;
		sinVal = Single.CopySign(sinVal, x);

		// FastCos (degree-8 minimax on [0, π/2] ⊂ [0, π]): same x²
		var cosVal = 0.0003538394f;
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.0041666418f);
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, 0.041666666f);
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.5f);
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, 1.0f);

		return sinVal / cosVal;
	}

	// ── double scalar implementations ────────────────────────────────────────

	/// <summary>
	/// Exact mirror of TanFunctionOptimizer's GenerateFastTanMethodDouble.
	/// NaN/Inf guards + Round range reduction + asymptote fallback at |x|>1.4 + rational P/Q.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double CurrentFastTanDouble(double x)
	{
		if (Double.IsNaN(x)) return Double.NaN;
		if (Double.IsInfinity(x)) return Double.NaN;

		const double InvPi = 1.0 / Double.Pi;

		var quotient = Double.Round(x * InvPi);
		var xReduced = Double.FusedMultiplyAdd(-quotient, Double.Pi, x);

		// Asymptote fallback — ~11 % of inputs hit this slow path
		var absX = Double.Abs(xReduced);
		if (absX > 1.4)
			return Double.Tan(x);

		var x2 = xReduced * xReduced;

		var p1 = -0.13089944486966634;
		var p2 = 0.005405742881796775;
		var p3 = -0.00010606776596208569;
		var numerator = Double.FusedMultiplyAdd(p3, x2, p2);
		numerator = Double.FusedMultiplyAdd(numerator, x2, p1);
		numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
		numerator *= xReduced;

		var q1 = -0.46468849716162905;
		var q2 = 0.015893657956882884;
		var q3 = -0.00031920703894961204;
		var denominator = Double.FusedMultiplyAdd(q3, x2, q2);
		denominator = Double.FusedMultiplyAdd(denominator, x2, q1);
		denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);

		return numerator / denominator;
	}

	/// <summary>
	/// FastTanV2(double): fold to [0,π/4] + exact [2/3] Padé rational approximant.
	/// Coefficients from exact Padé matching: a₁=−4/33, a₂=1/495 | b₁=−5/11, b₂=2/99, b₃=−1/10395.
	/// No NaN/Inf guards; no asymptote fallback; max error ≈ 1e−5 on [0, π/4].
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastTanV2Double(double x)
	{
		const double InvPi = 1.0 / Double.Pi;
		const double HalfPi = Double.Pi * 0.5;
		const double QuarterPi = Double.Pi * 0.25;

		// Range reduce to (−π/2, π/2) — tan's period is π
		var quotient = Double.Round(x * InvPi);
		x = Double.FusedMultiplyAdd(-quotient, Double.Pi, x);

		var signX = x;
		x = Double.Abs(x);

		// Fold to [0, π/4] via cotangent identity
		var swap = x > QuarterPi;
		var xf = swap ? HalfPi - x : x;

		// [2/3] Padé: tan(x)/x ≈ (1 + a₁·x² + a₂·x⁴) / (1 + b₁·x² + b₂·x⁴ + b₃·x⁶)
		var x2 = xf * xf;
		var num = Double.FusedMultiplyAdd(1.0 / 495.0, x2, -4.0 / 33.0);
		num = Double.FusedMultiplyAdd(num, x2, 1.0);
		num *= xf;

		var den = Double.FusedMultiplyAdd(-1.0 / 10395.0, x2, 2.0 / 99.0);
		den = Double.FusedMultiplyAdd(den, x2, -5.0 / 11.0);
		den = Double.FusedMultiplyAdd(den, x2, 1.0);

		var t = num / den;

		if (swap) t = 1.0 / t;

		return Double.CopySign(t, signX);
	}

	/// <summary>
	/// FastTanV3(double): sin/cos decomposition sharing range reduction and x².
	/// Reduces to (−π/2, π/2) once; FastSin (deg-11, 6 FMA) + FastCos (deg-10, 5 FMA) on same x².
	/// Exactly 1 division; accuracy ≈ 4e−14 (matches FastSin/FastCos).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double FastTanV3Double(double x)
	{
		const double InvPi = 1.0 / Double.Pi;

		// Range reduce to (−π/2, π/2) once — tan period is π
		var quotient = Double.Round(x * InvPi);
		x = Double.FusedMultiplyAdd(-quotient, Double.Pi, x);

		// |x| ≤ π/2: no further folding needed; x² shared between sin and cos
		var absX = Double.Abs(x);
		var x2 = absX * absX;

		// FastSin (degree-11 minimax on [0, π/2]): 6 FMA + 1 mul
		var sinVal = 2.6019406621361745e-9;
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, -1.9839531932589676e-7);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, 8.3333333333216515e-6);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269836761127);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, 0.0083333333333332177);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, 1.0);
		sinVal *= absX;
		sinVal = Double.CopySign(sinVal, x);

		// FastCos (degree-10 minimax on [0, π/2] ⊂ [0, π]): 5 FMA — same x²
		var cosVal = -1.1940250944959890e-7;
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, 2.0876755527587203e-5);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.0013888888888739916);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, 0.041666666666666602);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.5);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, 1.0);

		return sinVal / cosVal;
	}
}




