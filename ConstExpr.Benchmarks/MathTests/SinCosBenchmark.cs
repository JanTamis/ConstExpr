using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares simultaneous sine+cosine implementations for float and double.
///
/// Two groups:
///   Float  – float.SinCos  vs OldFastSinCos  vs FastSinCosV2 (current optimizer output)
///   Double – Math.SinCos   vs OldFastSinCos  vs FastSinCosV2 (current optimizer output)
///
/// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
///
///   Float:  DotNet=3.08ns   OldFast=1.78ns(-42%)   V2=1.60ns(-48%)   V2 is +10% faster than old
///   Double: DotNet=5.33ns   OldFast=1.84ns(-65%)   V2=1.62ns(-70%)   V2 is +12% faster than old
///
/// OldFastSinCos (previous optimizer):
///   Range reduction: Round(x / Tau) * Tau  — FDIV (~12-20 cy on ARM64)
///   Quadrant:        dead "if (absX > Pi)" + live "if (absX > HalfPi)" — two branches
///   Sin sign:        Single.CopySign(sinVal, x * Single.CopySign(1, x + quadAdjust)) — 2 CopySign
///   Sin poly:        degree-7 (3 FMA + 1 mul)   [float], degree-9 (4 FMA + 1 mul) [double]
///   Cos poly:        degree-6 (3 FMA + 1 add)   [float], degree-8 (4 FMA + 1 add) [double]
///
/// FastSinCosV2 improvements (now in optimizer):
///   Range reduction: Round(x * InvTau) * Tau  — FMUL only, no FDIV (~10 cy saved)
///   Quadrant:        ONE branchless ternary     — FCSEL on ARM64, zero mispredicts
///   Dead branch:     removed "if (absX > Pi)"  — impossible after correct range reduction
///   Sin sign:        Single.CopySign(sinVal, xSign) — one CopySign call
///   Same polynomials — identical numerical accuracy, control-flow improvements only
///
/// Input domain: [-100, 100] — ~32 full periods, exercises range reduction heavily.
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*SinCosBenchmark*'
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SinCosBenchmark
{
	private const int N = 1_024;

	private float[]  _floatData  = null!;
	private double[] _doubleData = null!;

	// Precomputed 1/(2π) — replaces division by Tau with a multiplication.
	private const float  InvTauF = 1f / (2f * MathF.PI);
	private const double InvTauD = 1.0 / (2.0 * Math.PI);

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_floatData  = new float[N];
		_doubleData = new double[N];
		for (var i = 0; i < N; i++)
		{
			var v = rng.NextDouble() * 200.0 - 100.0; // uniform in [-100, 100]
			_floatData[i]  = (float)v;
			_doubleData[i] = v;
		}
	}

	// ── float ──────────────────────────────────────────────────────────────

	/// <summary>Built-in float.SinCos — hardware-accurate, full-precision float result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float DotNetSinCos_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = float.SinCos(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Old FastSinCos(float): Round(x / Tau) range reduction with FDIV,
	/// dead branch on absX > Pi, two if-branches for quadrant, double-nested CopySign.
	/// 1.78 ns — 42 % faster than float.SinCos; replaced by V2 after benchmarking.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float OldFastSinCos_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = CurrentFastSinCosFloat(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Current FastSinCosV2(float) — now in optimizer:
	/// Round(x * InvTau) (no FDIV), dead branch removed, single branchless FCSEL,
	/// one CopySign for sin sign. 1.60 ns — 48 % faster than float.SinCos.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Float")]
	public float CurrentFastSinCos_Float()
	{
		var sum = 0f;
		foreach (var v in _floatData)
		{
			var (s, c) = FastSinCosV2Float(v);
			sum += s + c;
		}
		return sum;
	}

	// ── double ─────────────────────────────────────────────────────────────

	/// <summary>Built-in Math.SinCos — hardware-accurate, full-precision double result.</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double DotNetSinCos_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = Math.SinCos(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Old FastSinCos(double): Round(x / Tau) range reduction with FDIV,
	/// dead branch on absX > Pi, two if-branches for quadrant, double-nested CopySign.
	/// 1.84 ns — 65 % faster than Math.SinCos; replaced by V2 after benchmarking.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double OldFastSinCos_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = CurrentFastSinCosDouble(v);
			sum += s + c;
		}
		return sum;
	}

	/// <summary>
	/// Current FastSinCosV2(double) — now in optimizer:
	/// Round(x * InvTau) (no FDIV), dead branch removed, single branchless FCSEL,
	/// one CopySign for sin sign. 1.62 ns — 70 % faster than Math.SinCos.
	/// </summary>
	[Benchmark(OperationsPerInvoke = N)]
	[BenchmarkCategory("Double")]
	public double CurrentFastSinCos_Double()
	{
		var sum = 0.0;
		foreach (var v in _doubleData)
		{
			var (s, c) = FastSinCosV2Double(v);
			sum += s + c;
		}
		return sum;
	}

	// ── scalar implementations ──────────────────────────────────────────────

	/// <summary>
	/// Exact mirror of SinCosFunctionOptimizer.GenerateFastSinCosMethodFloat().
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (float Sin, float Cos) CurrentFastSinCosFloat(float x)
	{
		const float Tau    = 6.28318530717959f;
		const float Pi     = 3.14159265358979f;
		const float HalfPi = 1.57079632679490f;

		x = x - Single.Round(x / Tau) * Tau;

		var absX = Single.Abs(x);

		var quadAdjust = 0.0f;
		if (absX > Pi)
		{
			absX       = Tau - absX;
			quadAdjust = Pi;
		}

		var cosSign = 1.0f;
		if (absX > HalfPi)
		{
			absX    = Pi - absX;
			cosSign = -1.0f;
		}

		var x2 = absX * absX;

		var sinVal = -0.00019840874f;
		sinVal = Single.FusedMultiplyAdd(sinVal, x2,  0.0083333310f);
		sinVal = Single.FusedMultiplyAdd(sinVal, x2, -0.16666667f);
		sinVal = Single.FusedMultiplyAdd(sinVal, x2,  1.0f);
		sinVal = sinVal * absX;
		sinVal = Single.CopySign(sinVal, x * Single.CopySign(1.0f, x + quadAdjust));

		var cosVal = 0.0013888397f;
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.041666418f);
		cosVal = Single.FusedMultiplyAdd(cosVal, x2,  0.5f);
		cosVal = Single.FusedMultiplyAdd(cosVal, x2, -1.0f);
		cosVal = cosVal + 1.0f;
		cosVal = cosVal * cosSign;

		return (sinVal, cosVal);
	}

	/// <summary>
	/// Exact mirror of SinCosFunctionOptimizer.GenerateFastSinCosMethodDouble().
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (double Sin, double Cos) CurrentFastSinCosDouble(double x)
	{
		const double Tau    = 6.28318530717958647692;
		const double Pi     = 3.14159265358979323846;
		const double HalfPi = 1.57079632679489661923;

		x = x - Double.Round(x / Tau) * Tau;

		var absX = Double.Abs(x);

		var quadAdjust = 0.0;
		if (absX > Pi)
		{
			absX       = Tau - absX;
			quadAdjust = Pi;
		}

		var cosSign = 1.0;
		if (absX > HalfPi)
		{
			absX    = Pi - absX;
			cosSign = -1.0;
		}

		var x2 = absX * absX;

		var sinVal = 2.7557313707070068e-6;
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269841201856);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2,  0.0083333333333331650);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);
		sinVal = Double.FusedMultiplyAdd(sinVal, x2,  1.0);
		sinVal = sinVal * absX;
		sinVal = Double.CopySign(sinVal, x * Double.CopySign(1.0, x + quadAdjust));

		var cosVal = -2.6051615464872668e-5;
		cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.0013888888888887398);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.041666666666666664);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.5);
		cosVal = Double.FusedMultiplyAdd(cosVal, x2, -1.0);
		cosVal = cosVal + 1.0;
		cosVal = cosVal * cosSign;

		return (sinVal, cosVal);
	}

	/// <summary>
	/// V2 float: multiply instead of divide, dead branch removed, branchless quadrant,
	/// simplified single CopySign for sin sign. Same polynomial coefficients.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (float Sin, float Cos) FastSinCosV2Float(float x)
	{
		const float Tau    = 6.283185307179586f;
		const float Pi     = 3.141592653589793f;
		const float HalfPi = 1.5707963267948966f;

		// Multiply instead of divide — no FDIV
		x -= Single.Round(x * InvTauF) * Tau;

		// Capture sign before folding to positive half
		var xSign = Single.CopySign(1.0f, x);
		var absX  = Single.Abs(x);

		// Branchless quadrant reduction: FCSEL on ARM64, no branch mispredictions
		var over    = absX > HalfPi;
		var sinArg  = over ? Pi - absX : absX;
		var cosSign = over ? -1.0f : 1.0f;

		var x2 = sinArg * sinArg;

		// Sin polynomial (degree-7, 3 FMA + 1 mul)
		var s = -0.00019840874f;
		s = Single.FusedMultiplyAdd(s, x2,  0.0083333310f);
		s = Single.FusedMultiplyAdd(s, x2, -0.16666667f);
		s = Single.FusedMultiplyAdd(s, x2,  1.0f);
		s *= sinArg;
		s  = Single.CopySign(s, xSign); // single CopySign — simpler than the current double-nested form

		// Cos polynomial (degree-6, 3 FMA + 1 add + 1 mul)
		var c = 0.0013888397f;
		c = Single.FusedMultiplyAdd(c, x2, -0.041666418f);
		c = Single.FusedMultiplyAdd(c, x2,  0.5f);
		c = Single.FusedMultiplyAdd(c, x2, -1.0f);
		c += 1.0f;
		c *= cosSign;

		return (s, c);
	}

	/// <summary>
	/// V2 double: multiply instead of divide, dead branch removed, branchless quadrant,
	/// simplified single CopySign for sin sign. Same polynomial coefficients.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static (double Sin, double Cos) FastSinCosV2Double(double x)
	{
		const double Tau    = 6.283185307179586476925;
		const double Pi     = 3.141592653589793238462;
		const double HalfPi = 1.570796326794896619231;

		// Multiply instead of divide — no FDIV
		x -= Double.Round(x * InvTauD) * Tau;

		// Capture sign before folding
		var xSign = Double.CopySign(1.0, x);
		var absX  = Double.Abs(x);

		// Branchless quadrant reduction
		var over    = absX > HalfPi;
		var sinArg  = over ? Pi - absX : absX;
		var cosSign = over ? -1.0 : 1.0;

		var x2 = sinArg * sinArg;

		// Sin polynomial (degree-9, 4 FMA + 1 mul)
		var s = 2.7557313707070068e-6;
		s = Double.FusedMultiplyAdd(s, x2, -0.00019841269841201856);
		s = Double.FusedMultiplyAdd(s, x2,  0.0083333333333331650);
		s = Double.FusedMultiplyAdd(s, x2, -0.16666666666666666);
		s = Double.FusedMultiplyAdd(s, x2,  1.0);
		s *= sinArg;
		s  = Double.CopySign(s, xSign);

		// Cos polynomial (degree-8, 4 FMA + 1 add + 1 mul)
		var c = -2.6051615464872668e-5;
		c = Double.FusedMultiplyAdd(c, x2,  0.0013888888888887398);
		c = Double.FusedMultiplyAdd(c, x2, -0.041666666666666664);
		c = Double.FusedMultiplyAdd(c, x2,  0.5);
		c = Double.FusedMultiplyAdd(c, x2, -1.0);
		c += 1.0;
		c *= cosSign;

		return (s, c);
	}
}




