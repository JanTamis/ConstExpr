using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares double.AsinPi / float.AsinPi (IFloatingPointIeee754) against FastAsinPi variants
/// from AsinPiFunctionOptimizer. AsinPi(x) = asin(x) / π, result in [-0.5, 0.5].
///
/// Two type groups are benchmarked independently:
///   Float  – float.AsinPi  vs FastAsinPi(float)  vs FastAsinPiV2(float)  vs FastAsinPiV3(float)
///   Double – double.AsinPi vs FastAsinPi(double) vs FastAsinPiV2(double) vs FastAsinPiV3(double)
///
/// Variants:
///   FastAsinPi   – CURRENT optimizer output — branched at |x|&lt;0.5:
///                  Small: 2-term Taylor (x + x³/6)/π  — cheap, no sqrt (error ≈ 2.8e-3 at |x|=0.5)
///                  Large: A&amp;S §4.4.45 poly + sqrt → FMA(−p, 1/π, 0.5)
///   FastAsinPiV2 – branchless: A&amp;S poly for all |x|, always pays sqrt cost
///   FastAsinPiV3 – branchless with 1/π pre-absorbed into polynomial coefficients,
///                  eliminates final 1/π multiply (uses cheap sub 0.5−p instead)
///
/// Benchmark results (Apple M4 Pro, .NET 10, ARM64 RyuJIT):
///   Float:  DotNet=2.601 ns | FastAsinPi=1.098 ns (−58%) ← FASTEST | V2=1.155 ns | V3=1.156 ns
///   Double: DotNet=3.316 ns | FastAsinPi=1.001 ns (−70%) ← FASTEST | V2=1.150 ns | V3=1.149 ns
///
/// Conclusion: The current branching implementation is the fastest. The |x|&lt;0.5 branch is taken
/// ~50% of the time on uniform data, avoiding the expensive sqrt entirely for those calls.
/// V2/V3 always pay the full A&amp;S poly + sqrt cost, making them slower despite fewer branches.
/// V2 ≈ V3 — pre-absorbing 1/π into the polynomial saves a multiply but doesn't measurably help.
///
/// Accuracy note: The Taylor small-branch has error ≈ 2.8e-3 at |x|=0.5, which is 500× worse
/// than the A&amp;S large-branch (≈ 5.4e-6). Acceptable for FastMath mode.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*AsinPiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class AsinPiBenchmark
{
	// 1 024 values uniformly distributed over the AsinPi domain [-1, 1].
	private const int N = 1_024;
	private float[] _floatData = null!;
	private double[] _doubleData = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData = new float[N];
		_doubleData = new double[N];

		for (var i = 0; i < N; i++)
		{
			var v = rng.NextDouble() * 2.0 - 1.0; // uniform in [-1, 1]
			_floatData[i] = (float) v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in float.AsinPi (IFloatingPointIeee754) — hardware-accurate, full-precision result.
	/// Equivalent to MathF.Asin(x) / MathF.PI but dispatched through generic math.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetAsinPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += float.AsinPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastAsinPi(float) from AsinPiFunctionOptimizer — 2 clamps + 2 branches:
	///   |x| &lt; 0.5  → 2-term Taylor: (x + x³/6) / π       (error ≈ 2.8e-3 at |x|=0.5)
	///   |x| ≥ 0.5  → A&amp;S §4.4.45 poly + sqrt + FMA(−p, 1/π, 0.5)
	/// Clamps and branch mispredictions add overhead on random data.
	/// The Taylor branch is also significantly inaccurate near x=0.5.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAsinPi_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += CurrentFastAsinPiFloat(v);
		return sum;
	}

	/// <summary>
	/// FastAsinPiV2(float) — branchless: A&amp;S §4.4.45 minimax polynomial for all |x|.
	/// asinPi(x) = sign(x) · (0.5 − sqrt(1−|x|) · poly(|x|) / π).
	/// FMA(−p, 1/π, 0.5) fuses the π-division and subtraction in one instruction.
	/// No clamps, no conditional branches — eliminates misprediction overhead.
	/// Max error ≈ 5.4e-6 (same A&amp;S polynomial, uniform on [−1,1]).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAsinPiV2_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += BranchlessFastAsinPiFloat(v);
		return sum;
	}

	/// <summary>
	/// FastAsinPiV3(float) — branchless with 1/π pre-absorbed into coefficients.
	/// Uses d_n = c_n / π so the polynomial evaluates acosPi(|x|)/sqrt(1−|x|) directly.
	/// The final divide-by-π is gone; replaced with a cheap subtraction: result = 0.5f − p.
	/// Saves one scalar multiply at the end of the Horner chain.
	/// Coefficients: d0=0.4999935, d1=−0.06753755, d2=0.02363300, d3=−0.00596061.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAsinPiV3_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
			sum += PreDividedFastAsinPiFloat(v);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Built-in double.AsinPi (IFloatingPointIeee754) — hardware-accurate, full-precision result.
	/// Equivalent to Math.Asin(x) / Math.PI but dispatched through generic math.
	/// </summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetAsinPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += double.AsinPi(v);
		return sum;
	}

	/// <summary>
	/// Current FastAsinPi(double) from AsinPiFunctionOptimizer — 2 clamps + 2 branches:
	///   |x| &lt; 0.5  → 2-term Taylor / π        (error ≈ 2.8e-3 at |x|=0.5)
	///   |x| ≥ 0.5  → A&amp;S §4.4.45 poly + sqrt + FMA(−p, 1/π, 0.5)
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAsinPi_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += CurrentFastAsinPiDouble(v);
		return sum;
	}

	/// <summary>
	/// FastAsinPiV2(double) — branchless: A&amp;S §4.4.45 minimax polynomial for all |x|.
	/// No clamps, no conditional branches.
	/// Max error ≈ 5.4e-6 (A&amp;S polynomial coefficients in double arithmetic).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAsinPiV2_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += BranchlessFastAsinPiDouble(v);
		return sum;
	}

	/// <summary>
	/// FastAsinPiV3(double) — branchless with 1/π pre-absorbed into coefficients.
	/// Saves one multiply; final step is a cheap subtraction 0.5 − p.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAsinPiV3_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
			sum += PreDividedFastAsinPiDouble(v);
		return sum;
	}

	// ── current implementations (mirror of AsinPiFunctionOptimizer generated code) ──

	private static float CurrentFastAsinPiFloat(float x)
	{
		if (x < -1.0f) x = -1.0f;
		if (x > 1.0f) x = 1.0f;

		var xa = Single.Abs(x);

		if (xa < 0.5f)
		{
			var x2 = xa * xa;
			var ret = 0.16666667f;
			ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);
			ret = ret * xa * 0.31830988618379067f;
			return Single.CopySign(ret, x);
		}
		else
		{
			var onemx = 1.0f - xa;
			var sqrtOnemx = Single.Sqrt(onemx);
			var ret = -0.0187293f;
			ret = Single.FusedMultiplyAdd(ret, xa, 0.0742610f);
			ret = Single.FusedMultiplyAdd(ret, xa, -0.2121144f);
			ret = Single.FusedMultiplyAdd(ret, xa, 1.5707288f);
			ret *= sqrtOnemx;
			ret = Single.FusedMultiplyAdd(-ret, 0.31830988618379067f, 0.5f);
			return Single.CopySign(ret, x);
		}
	}

	private static double CurrentFastAsinPiDouble(double x)
	{
		if (x < -1.0) x = -1.0;
		if (x > 1.0) x = 1.0;

		var xa = Double.Abs(x);

		if (xa < 0.5)
		{
			var x2 = xa * xa;
			var ret = 0.16666666666666666;
			ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
			ret = ret * xa * 0.31830988618379067;
			return Double.CopySign(ret, x);
		}
		else
		{
			var onemx = 1.0 - xa;
			var sqrtOnemx = Double.Sqrt(onemx);
			var ret = -0.0187293;
			ret = Double.FusedMultiplyAdd(ret, xa, 0.0742610);
			ret = Double.FusedMultiplyAdd(ret, xa, -0.2121144);
			ret = Double.FusedMultiplyAdd(ret, xa, 1.5707288);
			ret *= sqrtOnemx;
			ret = Double.FusedMultiplyAdd(-ret, 0.31830988618379067, 0.5);
			return Double.CopySign(ret, x);
		}
	}

	// ── improved implementations ────────────────────────────────────────────

	/// <summary>
	/// Branchless: A&amp;S §4.4.45 minimax poly for all |x| ∈ [0,1].
	/// asinPi(x) = sign(x) · FMA(−sqrt(1−|x|)·poly(|x|), 1/π, 0.5).
	/// Single sqrt, 3 FMAs, 1 FMA for π-division — no branches, no clamps.
	/// </summary>
	private static float BranchlessFastAsinPiFloat(float x)
	{
		var ax = Single.Abs(x);
		var p = Single.FusedMultiplyAdd(-0.0187293f, ax, 0.0742610f);
		p = Single.FusedMultiplyAdd(p, ax, -0.2121144f);
		p = Single.FusedMultiplyAdd(p, ax, 1.5707288f);
		p *= Single.Sqrt(1.0f - ax);                                // p ≈ acos(|x|)
		var result = Single.FusedMultiplyAdd(-p, 0.31830988618379067f, 0.5f); // 0.5 − p/π
		return Single.CopySign(result, x);
	}

	private static double BranchlessFastAsinPiDouble(double x)
	{
		var ax = Double.Abs(x);
		var p = Double.FusedMultiplyAdd(-0.0187293, ax, 0.0742610);
		p = Double.FusedMultiplyAdd(p, ax, -0.2121144);
		p = Double.FusedMultiplyAdd(p, ax, 1.5707288);
		p *= Double.Sqrt(1.0 - ax);                                       // p ≈ acos(|x|)
		var result = Double.FusedMultiplyAdd(-p, 0.31830988618379067, 0.5); // 0.5 − p/π
		return Double.CopySign(result, x);
	}

	/// <summary>
	/// Branchless with 1/π pre-absorbed into polynomial coefficients:
	///   d_n = c_n / π  (compile-time constants).
	/// The polynomial evaluates acosPi(|x|) / sqrt(1−|x|) directly, so the
	/// final step is a plain subtraction 0.5 − p (no multiply-by-1/π needed).
	///
	/// Float coefficients (A&amp;S §4.4.45 divided by π):
	///   d0 = 1.5707288  / π ≈ 0.4999935
	///   d1 = −0.2121144 / π ≈ −0.06753755
	///   d2 = 0.0742610  / π ≈ 0.02363300
	///   d3 = −0.0187293 / π ≈ −0.00596061
	/// </summary>
	private static float PreDividedFastAsinPiFloat(float x)
	{
		var ax = Single.Abs(x);
		var p = Single.FusedMultiplyAdd(-0.00596061f, ax, 0.02363300f);  // d3·ax + d2
		p = Single.FusedMultiplyAdd(p, ax, -0.06753755f);                 // ·ax + d1
		p = Single.FusedMultiplyAdd(p, ax, 0.4999935f);                   // ·ax + d0
		p *= Single.Sqrt(1.0f - ax);                                       // p ≈ acosPi(|x|)
		var result = 0.5f - p;                                             // asinPi(|x|)
		return Single.CopySign(result, x);
	}

	private static double PreDividedFastAsinPiDouble(double x)
	{
		var ax = Double.Abs(x);
		// Double coefficients: d_n = c_n / π
		var p = Double.FusedMultiplyAdd(-0.00596061, ax, 0.02363300);   // d3·ax + d2
		p = Double.FusedMultiplyAdd(p, ax, -0.06753755);                  // ·ax + d1
		p = Double.FusedMultiplyAdd(p, ax, 0.4999935);                    // ·ax + d0
		p *= Double.Sqrt(1.0 - ax);                                        // p ≈ acosPi(|x|)
		var result = 0.5 - p;                                              // asinPi(|x|)
		return Double.CopySign(result, x);
	}
}




