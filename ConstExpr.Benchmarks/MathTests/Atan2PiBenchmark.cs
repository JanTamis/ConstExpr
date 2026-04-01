using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares the built-in double/float.Atan2Pi against the FastAtan2Pi approximation generated
/// by Atan2PiFunctionOptimizer and two improved scalar alternatives.
///
/// Two type groups are benchmarked independently:
///   Float  – float.Atan2Pi  vs CurrentFastAtan2Pi(float)  vs FastAtan2PiV2(float)  vs FastAtan2PiV3(float)
///   Double – double.Atan2Pi vs CurrentFastAtan2Pi(double) vs FastAtan2PiV2(double) vs FastAtan2PiV3(double)
///
/// Key design decisions for V2/V3:
///   Atan2Pi(y, x) = Atan2(y, x) / π.  Instead of calling FastAtan2 and then multiplying by 1/π,
///   we absorb the 1/π factor directly into the polynomial coefficients and into the quadrant-
///   correction constants (π/2 → 0.5, π → 1.0).  This removes one multiply from the hot path.
///
///   V2 (float + double): branchless octant reduction + 5-term A&amp;S §4.4.43 minimax polynomial
///     with coefficients pre-divided by π.  Quadrant corrections use 0.5 and 1.0 instead of π/2
///     and π.  Max absolute error ≈ 3.5e-6 (≈ 1.1e-5 rad / π).
///   V3 (float): 2-term ultra-fast polynomial with π-scaled coefficients.  1 FMA + 1 mul in core.
///     Max absolute error ≈ 1.6e-3 (≈ 5e-3 rad / π).
///   V3 (double): half-angle t = a/(1+√(1+a²)) reduces a∈[0,1] to t∈[0,tan(π/8)]; 8-term
///     Taylor series, result scaled by 2/π instead of the usual 2.  Max absolute error ≈ 1.3e-8.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*Atan2PiBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Atan2PiBenchmark
{
	// 1 024 (y, x) pairs uniformly distributed over [-2, 2] × [-2, 2].
	// All four quadrants are represented so branch-prediction pressure is realistic.
	private const int N = 1_024;
	private float[]  _floatY  = null!;
	private float[]  _floatX  = null!;
	private double[] _doubleY = null!;
	private double[] _doubleX = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatY  = new float[N];
		_floatX  = new float[N];
		_doubleY = new double[N];
		_doubleX = new double[N];

		for (var i = 0; i < N; i++)
		{
			var y = rng.NextDouble() * 4.0 - 2.0; // uniform in [-2, 2]
			var x = rng.NextDouble() * 4.0 - 2.0;
			_floatY[i]  = (float)y;
			_floatX[i]  = (float)x;
			_doubleY[i] = y;
			_doubleX[i] = x;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.Atan2Pi — IEEE 754-2019, hardware-assisted, full-precision result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetAtan2Pi_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += float.Atan2Pi(_floatY[i], _floatX[i]);
		return sum;
	}

	/// <summary>
	/// Current FastAtan2Pi(float) from Atan2PiFunctionOptimizer — branched Padé [2/2] rational
	/// approximant (same formula as FastAtan2), result divided by π at the end.
	/// Max absolute error ≈ 8e-3 (≈ 2.5e-2 rad / π).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastAtan2Pi_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += CurrentFastAtan2PiFloat(_floatY[i], _floatX[i]);
		return sum;
	}

	/// <summary>
	/// FastAtan2PiV2(float) — branchless octant reduction + 5-term A&amp;S §4.4.43 minimax polynomial.
	/// Coefficients are pre-divided by π, and quadrant corrections use 0.5 / 1.0 instead of π/2 / π.
	/// Saves one multiply versus computing FastAtan2 and then dividing.
	/// Max absolute error ≈ 3.5e-6.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAtan2PiV2_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += FastAtan2PiFloatV2(_floatY[i], _floatX[i]);
		return sum;
	}

	/// <summary>
	/// FastAtan2PiV3(float) — branchless octant reduction + 2-term polynomial.
	/// atan2pi(a) ≈ a*((1.0583981 − 0.273*a) / π) with pre-scaled coefficients; 1 FMA + 1 mul.
	/// Fastest scalar variant; max absolute error ≈ 1.6e-3.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float FastAtan2PiV3_Float()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += FastAtan2PiFloatV3(_floatY[i], _floatX[i]);
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in double.Atan2Pi — IEEE 754-2019, hardware-assisted, full-precision result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetAtan2Pi_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += double.Atan2Pi(_doubleY[i], _doubleX[i]);
		return sum;
	}

	/// <summary>
	/// Current FastAtan2Pi(double) from Atan2PiFunctionOptimizer — branched Padé [2/2] rational
	/// approximant, result multiplied by 1/π.
	/// Max absolute error ≈ 8e-3 (≈ 2.5e-2 rad / π).
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastAtan2Pi_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += CurrentFastAtan2PiDouble(_doubleY[i], _doubleX[i]);
		return sum;
	}

	/// <summary>
	/// FastAtan2PiV2(double) — branchless octant reduction + 5-term A&amp;S §4.4.43 minimax polynomial
	/// with coefficients pre-divided by π.  Quadrant corrections use 0.5 / 1.0.
	/// Max absolute error ≈ 3.5e-6.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAtan2PiV2_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += FastAtan2PiDoubleV2(_doubleY[i], _doubleX[i]);
		return sum;
	}

	/// <summary>
	/// FastAtan2PiV3(double) — branchless octant reduction + half-angle t = a/(1+√(1+a²)) +
	/// 8-term Taylor series, final scale 2/π instead of 2.
	/// One sqrt, no branches in the core; max absolute error ≈ 1.3e-8.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double FastAtan2PiV3_Double()
	{
		var sum = 0.0;
		for (var i = 0; i < N; i++)
			sum += FastAtan2PiDoubleV3(_doubleY[i], _doubleX[i]);
		return sum;
	}

	// ── current implementations (exact mirror of Atan2PiFunctionOptimizer generated code) ──

	private static float CurrentFastAtan2PiFloat(float y, float x)
	{
		if (float.IsNaN(y) || float.IsNaN(x)) return float.NaN;
		if (y == 0.0f && x == 0.0f) return 0.0f;

		var absY = float.Abs(y);
		var absX = float.Abs(x);
		float angle;

		if (absX >= absY)
		{
			var z  = y / x;
			var z2 = z * z;
			var z4 = z2 * z2;
			var numerator   = float.FusedMultiplyAdd(4.0f, z2, 15.0f);
			numerator      *= z;
			var denominator = float.FusedMultiplyAdd(9.0f, z2, 15.0f);
			denominator     = z4 + denominator;
			angle = numerator / denominator;
			if (float.IsNegative(x))
				angle = y >= 0.0f ? float.Pi + angle : angle - float.Pi;
		}
		else
		{
			var z  = x / y;
			var z2 = z * z;
			var z4 = z2 * z2;
			var numerator   = float.FusedMultiplyAdd(4.0f, z2, 15.0f);
			numerator      *= z;
			var denominator = float.FusedMultiplyAdd(9.0f, z2, 15.0f);
			denominator     = z4 + denominator;
			var baseAngle   = numerator / denominator;
			angle = y >= 0.0f ? float.Pi / 2 - baseAngle : -float.Pi / 2 - baseAngle;
		}

		return angle * (1f / float.Pi);
	}

	private static double CurrentFastAtan2PiDouble(double y, double x)
	{
		if (double.IsNaN(y) || double.IsNaN(x)) return double.NaN;
		if (y == 0.0 && x == 0.0) return 0.0;

		var absY = double.Abs(y);
		var absX = double.Abs(x);
		double angle;

		if (absX >= absY)
		{
			var z  = y / x;
			var z2 = z * z;
			var z4 = z2 * z2;
			var numerator   = double.FusedMultiplyAdd(4.0, z2, 15.0);
			numerator      *= z;
			var denominator = double.FusedMultiplyAdd(9.0, z2, 15.0);
			denominator     = z4 + denominator;
			angle = numerator / denominator;
			if (double.IsNegative(x))
				angle = y >= 0.0 ? double.Pi + angle : angle - double.Pi;
		}
		else
		{
			var z  = x / y;
			var z2 = z * z;
			var z4 = z2 * z2;
			var numerator   = double.FusedMultiplyAdd(4.0, z2, 15.0);
			numerator      *= z;
			var denominator = double.FusedMultiplyAdd(9.0, z2, 15.0);
			denominator     = z4 + denominator;
			var baseAngle   = numerator / denominator;
			angle = y >= 0.0 ? double.Pi / 2 - baseAngle : -double.Pi / 2 - baseAngle;
		}

		return angle * (1.0 / double.Pi);
	}

	// ── improved implementations ────────────────────────────────────────────

	/// <summary>
	/// Branchless octant reduction + A&amp;S §4.4.43 5-term minimax polynomial.
	/// Coefficients are pre-multiplied by 1/π so the final /π multiply is absorbed.
	/// Quadrant corrections use 0.5 (=π/2/π) and 1.0 (=π/π) directly.
	/// </summary>
	private static float FastAtan2PiFloatV2(float y, float x)
	{
		if (float.IsNaN(y) || float.IsNaN(x)) return float.NaN;
		var absX = float.Abs(x);
		var absY = float.Abs(y);
		var maxV = float.Max(absX, absY);
		if (maxV == 0f) return 0f;

		var a = float.Min(absX, absY) / maxV; // a ∈ [0, 1]
		var u = a * a;

		// A&S §4.4.43 minimax coefficients pre-divided by π — evaluates atan(a)/(π·a) as poly in u=a²
		// Original: {0.9998660, -0.3302995, 0.1801410, -0.0851330, 0.0208351}
		// Scaled:   cᵢ / π
		var p = float.FusedMultiplyAdd(u,  0.00663190f, -0.02709817f);
		p      = float.FusedMultiplyAdd(u, p,             0.05733816f);
		p      = float.FusedMultiplyAdd(u, p,            -0.10514577f);
		p      = float.FusedMultiplyAdd(u, p,             0.31826957f);
		p     *= a;

		// Quadrant corrections in units of turns (0.5 = π/2 / π, 1.0 = π / π)
		p = absY > absX ?  0.5f - p : p;
		p = x    < 0f  ?  1.0f - p : p;
		p = y    < 0f  ?    -p     : p;
		return p;
	}

	/// <summary>
	/// Ultra-fast: 1 FMA + 1 mul in the polynomial core, coefficients scaled by 1/π.
	/// atan2pi(a) ≈ a*((1.0583981 − 0.273*a) / π)  →  a*(0.33698−0.08691*a)
	/// </summary>
	private static float FastAtan2PiFloatV3(float y, float x)
	{
		if (float.IsNaN(y) || float.IsNaN(x)) return float.NaN;
		var absX = float.Abs(x);
		var absY = float.Abs(y);
		var maxV = float.Max(absX, absY);
		if (maxV == 0f) return 0f;

		var a = float.Min(absX, absY) / maxV;

		// 2-term poly with 1/π scaling absorbed: (π/4+0.273)/π ≈ 0.33698, 0.273/π ≈ 0.08691
		var p = float.FusedMultiplyAdd(-0.08691f, a, 0.33698f) * a;

		p = absY > absX ?  0.5f - p : p;
		p = x    < 0f  ?  1.0f - p : p;
		p = y    < 0f  ?    -p     : p;
		return p;
	}

	/// <summary>
	/// Same branchless octant reduction + A&amp;S §4.4.43 polynomial as V2 float, in double arithmetic,
	/// coefficients pre-divided by π.  Max absolute error ≈ 3.5e-6.
	/// </summary>
	private static double FastAtan2PiDoubleV2(double y, double x)
	{
		if (double.IsNaN(y) || double.IsNaN(x)) return double.NaN;
		var absX = double.Abs(x);
		var absY = double.Abs(y);
		var maxV = double.Max(absX, absY);
		if (maxV == 0.0) return 0.0;

		var a = double.Min(absX, absY) / maxV;
		var u = a * a;

		// A&S §4.4.43 coefficients / π
		var p = double.FusedMultiplyAdd(u,  0.00663190, -0.02709817);
		p      = double.FusedMultiplyAdd(u, p,           0.05733816);
		p      = double.FusedMultiplyAdd(u, p,          -0.10514577);
		p      = double.FusedMultiplyAdd(u, p,           0.31826957);
		p     *= a;

		p = absY > absX ?  0.5  - p : p;
		p = x    < 0.0  ?  1.0  - p : p;
		p = y    < 0.0  ?   -p      : p;
		return p;
	}

	/// <summary>
	/// High-accuracy double: half-angle t = a/(1+√(1+a²)) maps a∈[0,1] → t∈[0,tan(π/8)].
	/// 8-term Horner Taylor series, scaled by 2/π (instead of 2) so the output is in turns.
	/// Max absolute error ≈ 1.3e-8.
	/// </summary>
	private static double FastAtan2PiDoubleV3(double y, double x)
	{
		if (double.IsNaN(y) || double.IsNaN(x)) return double.NaN;
		var absX = double.Abs(x);
		var absY = double.Abs(y);
		var maxV = double.Max(absX, absY);
		if (maxV == 0.0) return 0.0;

		var a = double.Min(absX, absY) / maxV;

		// Half-angle reduction: atan(a) = 2·atan(t),  t = a/(1+√(1+a²))
		var t = a / (1.0 + Math.Sqrt(1.0 + a * a));
		var u = t * t;

		// 8-term Horner Taylor series: atan(t)/t = Σ (−u)^n/(2n+1)
		var p = double.FusedMultiplyAdd(u, -1.0 / 15.0,  1.0 / 13.0);
		p      = double.FusedMultiplyAdd(u, p,           -1.0 / 11.0);
		p      = double.FusedMultiplyAdd(u, p,            1.0 /  9.0);
		p      = double.FusedMultiplyAdd(u, p,           -1.0 /  7.0);
		p      = double.FusedMultiplyAdd(u, p,            1.0 /  5.0);
		p      = double.FusedMultiplyAdd(u, p,           -1.0 /  3.0);
		p      = double.FusedMultiplyAdd(u, p,            1.0);
		// Scale by 2/π instead of 2 to get the result directly in turns: atan2pi(a) = 2·atan(t)/π
		p = (2.0 / Math.PI) * t * p;

		p = absY > absX ?  0.5  - p : p;
		p = x    < 0.0  ?  1.0  - p : p;
		p = y    < 0.0  ?   -p      : p;
		return p;
	}
}

