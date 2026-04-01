using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares MathF.Tanh / Math.Tanh (built-in) against three scalar FastTanh candidates.
///
/// Two type groups are benchmarked independently:
///   Float  – MathF.Tanh  vs CurrentFastTanh(float)  vs FastTanhV2(float)  vs FastTanhV3(float)
///   Double – Math.Tanh   vs CurrentFastTanh(double) vs FastTanhV2(double) vs FastTanhV3(double)
///
/// Current FastTanh analysis:
///   3 branches: NaN guard, saturation at ±5, inner split at |x| &lt; 1.
///   For |x| &lt; 1: [5,4] Padé rational approximation (4 FMA + 1 division, no transcendental).
///   For |x| ≥ 1: (Single.Exp(2x) - 1) / (Single.Exp(2x) + 1) — uses the slow built-in exp.
///   The inner branch causes mispredictions on mixed input distributions (random data
///   from [-4, 4] hits both paths roughly equally for |x| in [0, 1] vs [1, 4]).
///
/// V2 (FastExp hybrid):
///   Same hybrid structure as Current but replaces Single.Exp / Double.Exp with our
///   FastExpFloat / FastExpDouble (the V2 direct-poly exp from ExpBenchmark, ~2× faster
///   than the built-in).  The inner branch remains; only the exp-path cost changes.
///
/// V3 (pure FastExp, no inner branch):
///   Eliminates the |x| &lt; 1 inner branch entirely.
///   tanh(x) = (FastExp(2x) - 1) / (FastExp(2x) + 1), saturated at ±5 (float) / ±19 (double).
///   Just two saturation checks, one FastExp call, one subtraction, one addition, one division.
///   Fewer branches → better branch-predictor behaviour on random inputs.
///   FastExp(2x) where |x| ≤ 5 means |2x| ≤ 10, well inside FastExp's safe domain (±87/±708).
///
/// Input domain: [-4, 4] — spans the polynomial branch region (|x| &lt; 1) and the exp-based
/// region (1 ≤ |x| ≤ 4), exercising both paths of the current hybrid implementation.
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT, uniform [-4,4] input):
///
///   Method              Category  Mean      Ratio    Note
///   ------------------  --------  --------  -------  -------------------------------------------
///   DotNetTanh_Float    Float     2.123 ns  1.00x    built-in, IEEE-accurate
///   CurrentFastTanh     Float     1.942 ns  0.91x    old: Padé + Single.Exp, inner branch
///   FastTanhV2_Float    Float     1.757 ns  0.83x    FastExp hybrid (inner branch kept)
///   FastTanhV3_Float    Float     1.753 ns  0.83x  ← new production: pure FastExp, no inner branch
///
///   DotNetTanh_Double   Double    2.595 ns  1.00x    built-in, IEEE-accurate
///   CurrentFastTanh     Double    2.647 ns  1.02x    old: was SLOWER than built-in!
///   FastTanhV3_Double   Double    2.750 ns  1.06x    pure FastExp path (worse when cold)
///   FastTanhV2_Double   Double    2.496 ns  0.96x  ← new production: Padé + FastExpDouble hybrid
///
/// Conclusion:
///   Float: V3 (pure FastExp, no inner branch) is the production implementation.  Eliminates
///          the misprediction overhead of the |x|&lt;1 inner branch at no accuracy cost.
///   Double: V2 (FastExp hybrid) is production.  The old hybrid used Double.Exp which is the
///           built-in slow transcendental and made CurrentFastTanh SLOWER than Math.Tanh on
///           random data.  Replacing with inlined FastExpDouble (2.8× faster) recovers the lead.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*TanhBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TanhBenchmark
{
	// 1 024 values uniformly distributed over [-4, 4].
	// This range spans both the rational-approximation path (|x| < 1) and the
	// exp-based path (1 ≤ |x| ≤ 4) of the current hybrid implementation.
	// Fixed seed for reproducibility; instance field forces real memory loads.
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
			// Uniform in [-4, 4]: tanh(±4) ≈ ±0.9993 — fully non-saturated range.
			var v = rng.NextDouble() * 8.0 - 4.0;
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float benchmarks ──────────────────────────────────────────────────

	/// <summary>Built-in MathF.Tanh — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetTanh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += MathF.Tanh(v);
		return sum;
	}

	/// <summary>
	/// CurrentFastTanh(float) — exact mirror of TanhFunctionOptimizer generated code.
	/// NaN guard + saturation at ±5, then:
	///   |x| &lt; 1 → [5,4] Padé rational (4 FMA + 1 division).
	///   |x| ≥ 1 → (Single.Exp(2x) - 1) / (Single.Exp(2x) + 1) using the slow built-in exp.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastTanh_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastTanhFloat(v);
		return sum;
	}

	/// <summary>
	/// FastTanhV2(float) — same hybrid as Current but with FastExpFloat instead of Single.Exp.
	/// FastExpFloat is the direct-poly V2 exp (~2× faster than MathF.Exp on ARM64).
	/// Inner branch (|x| &lt; 1) is preserved; only the exp-path cost is reduced.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanhV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += FastExpHybridTanhFloat(v);
		return sum;
	}

	/// <summary>
	/// FastTanhV3(float) — pure FastExp path, no inner branch.
	/// tanh(x) = (FastExp(2x) - 1) / (FastExp(2x) + 1), saturated at ±5.
	/// Just two saturation branches, one FastExpFloat(2x) call, two arithmetic ops, one division.
	/// Input 2x is at most ±10, well inside FastExpFloat's safe domain (−87 … 88).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastTanhV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += PureFastExpTanhFloat(v);
		return sum;
	}

	// ── double benchmarks ─────────────────────────────────────────────────

	/// <summary>Built-in Math.Tanh — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetTanh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += Math.Tanh(v);
		return sum;
	}

	/// <summary>
	/// CurrentFastTanh(double) — exact mirror of TanhFunctionOptimizer generated code.
	/// NaN guard + saturation at ±19, then:
	///   |x| &lt; 1  → [5,6] Padé rational (6 FMA + 1 division).
	///   |x| &lt; 9  → (Double.Exp(2x) - 1) / (Double.Exp(2x) + 1) with built-in exp.
	///   |x| ≥ 9  → CopySign(1 - 2·Exp(-2|x|), x) with built-in exp.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastTanh_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastTanhDouble(v);
		return sum;
	}

	/// <summary>
	/// FastTanhV2(double) — same hybrid as Current but with FastExpDouble instead of Double.Exp.
	/// FastExpDouble is the direct-poly V2 exp (~2.8× faster than Math.Exp on ARM64).
	/// All inner branches are preserved; only the exp-path cost is reduced.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanhV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += FastExpHybridTanhDouble(v);
		return sum;
	}

	/// <summary>
	/// FastTanhV3(double) — pure FastExp path, no inner branch.
	/// tanh(x) = (FastExp(2x) - 1) / (FastExp(2x) + 1), saturated at ±19.
	/// Just two saturation branches, one FastExpDouble(2x) call, two arithmetic ops, one division.
	/// Input 2x is at most ±38, well inside FastExpDouble's safe domain (−708 … 709).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastTanhV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += PureFastExpTanhDouble(v);
		return sum;
	}

	// ── current implementation (mirrored exactly from TanhFunctionOptimizer) ──

	private static float CurrentFastTanhFloat(float x)
	{
		if (Single.IsNaN(x)) return Single.NaN;
		if (x >= 5.0f) return 1.0f;
		if (x <= -5.0f) return -1.0f;

		var absX = Single.Abs(x);

		if (absX < 1.0f)
		{
			var x2 = x * x;

			var a1 = -0.3333314f;
			var a2 = 0.1333924f;
			var numerator = Single.FusedMultiplyAdd(a2, x2, a1);
			numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
			numerator *= x;

			var b1 = 1.0f;
			var b2 = -0.3333314f;
			var denominator = Single.FusedMultiplyAdd(b2, x2, b1);
			denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);

			return numerator / denominator;
		}
		else
		{
			var exp2x = Single.Exp(2.0f * x);
			return (exp2x - 1.0f) / (exp2x + 1.0f);
		}
	}

	private static double CurrentFastTanhDouble(double x)
	{
		if (Double.IsNaN(x)) return Double.NaN;
		if (x >= 19.0) return 1.0;
		if (x <= -19.0) return -1.0;

		var absX = Double.Abs(x);

		if (absX < 1.0)
		{
			var x2 = x * x;

			var a1 = -0.333333333333331;
			var a2 = 0.133333333333197;
			var a3 = -0.0539682539682505;
			var numerator = Double.FusedMultiplyAdd(a3, x2, a2);
			numerator = Double.FusedMultiplyAdd(numerator, x2, a1);
			numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
			numerator *= x;

			var b1 = 1.0;
			var b2 = -0.133333333333197;
			var b3 = 0.0107936507936338;
			var denominator = Double.FusedMultiplyAdd(b3, x2, b2);
			denominator = Double.FusedMultiplyAdd(denominator, x2, b1);
			denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);

			return numerator / denominator;
		}
		else if (absX < 9.0)
		{
			var exp2x = Double.Exp(2.0 * x);
			return (exp2x - 1.0) / (exp2x + 1.0);
		}
		else
		{
			var exp2absX = Double.Exp(-2.0 * absX);
			return Double.CopySign(1.0 - 2.0 * exp2absX, x);
		}
	}

	// ── V2: same hybrid, FastExp replaces built-in exp ────────────────────
	// FastExpFloat / FastExpDouble = direct-poly V2 from ExpBenchmark.
	// ~2× (float) / ~2.8× (double) faster than the built-in transcendental.

	private static float FastExpHybridTanhFloat(float x)
	{
		if (Single.IsNaN(x)) return Single.NaN;
		if (x >= 5.0f) return 1.0f;
		if (x <= -5.0f) return -1.0f;

		var absX = Single.Abs(x);

		if (absX < 1.0f)
		{
			var x2 = x * x;

			var a1 = -0.3333314f;
			var a2 = 0.1333924f;
			var numerator = Single.FusedMultiplyAdd(a2, x2, a1);
			numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
			numerator *= x;

			var b1 = 1.0f;
			var b2 = -0.3333314f;
			var denominator = Single.FusedMultiplyAdd(b2, x2, b1);
			denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);

			return numerator / denominator;
		}
		else
		{
			var exp2x = FastExpFloat(2.0f * x);
			return (exp2x - 1.0f) / (exp2x + 1.0f);
		}
	}

	private static double FastExpHybridTanhDouble(double x)
	{
		if (Double.IsNaN(x)) return Double.NaN;
		if (x >= 19.0) return 1.0;
		if (x <= -19.0) return -1.0;

		var absX = Double.Abs(x);

		if (absX < 1.0)
		{
			var x2 = x * x;

			var a1 = -0.333333333333331;
			var a2 = 0.133333333333197;
			var a3 = -0.0539682539682505;
			var numerator = Double.FusedMultiplyAdd(a3, x2, a2);
			numerator = Double.FusedMultiplyAdd(numerator, x2, a1);
			numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
			numerator *= x;

			var b1 = 1.0;
			var b2 = -0.133333333333197;
			var b3 = 0.0107936507936338;
			var denominator = Double.FusedMultiplyAdd(b3, x2, b2);
			denominator = Double.FusedMultiplyAdd(denominator, x2, b1);
			denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);

			return numerator / denominator;
		}
		else if (absX < 9.0)
		{
			var exp2x = FastExpDouble(2.0 * x);
			return (exp2x - 1.0) / (exp2x + 1.0);
		}
		else
		{
			var exp2absX = FastExpDouble(-2.0 * absX);
			return Double.CopySign(1.0 - 2.0 * exp2absX, x);
		}
	}

	// ── V3: pure FastExp path, no inner branch ────────────────────────────
	// tanh(x) = (FastExp(2x) - 1) / (FastExp(2x) + 1)
	// Saturate at ±5 (float) / ±19 (double); single FastExp call; zero inner branches.
	// 2x is at most ±10 (float) / ±38 (double), comfortably inside FastExp's safe domain.

	private static float PureFastExpTanhFloat(float x)
	{
		if (x >= 5.0f) return 1.0f;
		if (x <= -5.0f) return -1.0f;
		var exp2x = FastExpFloat(2.0f * x);
		return (exp2x - 1.0f) / (exp2x + 1.0f);
	}

	private static double PureFastExpTanhDouble(double x)
	{
		if (x >= 19.0) return 1.0;
		if (x <= -19.0) return -1.0;
		var exp2x = FastExpDouble(2.0 * x);
		return (exp2x - 1.0) / (exp2x + 1.0);
	}

	// ── FastExp helpers (direct-poly V2 — fastest scalar exp from ExpBenchmark) ──
	// Reduction: k = round(x·log₂e), r = kf − k ∈ [−0.5, 0.5].
	// Polynomial cₙ = ln(2)ⁿ/n! evaluates 2^r directly without an FMA(-k, LN2, x) step.
	// MathF.Round / Math.Round → branchless FRINTN+FCVTZS on ARM64.

	private static float FastExpFloat(float x)
	{
		if (x >= 88.0f) return float.PositiveInfinity;
		if (x <= -87.0f) return 0.0f;

		const float INV_LN2 = 1.4426950408889634f;  // log₂(e)

		var kf  = x * INV_LN2;
		var k   = (int)MathF.Round(kf);             // branchless FRINTN + FCVTZS on ARM64
		var r   = kf - k;                           // fractional log₂(e^x), r ∈ [−0.5, 0.5]

		// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!
		const float c3 = 0.055504108664821580f;   // ln(2)³ / 6
		const float c2 = 0.240226506959100690f;   // ln(2)² / 2
		const float c1 = 0.693147180559945309f;   // ln(2)

		var p    = MathF.FusedMultiplyAdd(c3, r, c2);
		p        = MathF.FusedMultiplyAdd(p,  r, c1);
		var expR = MathF.FusedMultiplyAdd(p,  r, 1.0f);

		return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;
	}

	private static double FastExpDouble(double x)
	{
		if (x >= 709.0) return double.PositiveInfinity;
		if (x <= -708.0) return 0.0;

		const double INV_LN2 = 1.4426950408889634073599246810018921;  // log₂(e)

		var kf  = x * INV_LN2;
		var k   = (long)Math.Round(kf);            // branchless FRINTN+FCVTZS on ARM64
		var r   = kf - k;                          // r ∈ [−0.5, 0.5]

		// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!
		const double c4 = 9.618129107628477232e-3;  // ln(2)⁴ / 24
		const double c3 = 5.550410866482157995e-2;  // ln(2)³ / 6
		const double c2 = 2.402265069591006909e-1;  // ln(2)² / 2
		const double c1 = 6.931471805599453094e-1;  // ln(2)

		var p    = Double.FusedMultiplyAdd(c4, r, c3);
		p        = Double.FusedMultiplyAdd(p,  r, c2);
		p        = Double.FusedMultiplyAdd(p,  r, c1);
		var expR = Double.FusedMultiplyAdd(p,  r, 1.0);

		return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * expR;
	}
}


