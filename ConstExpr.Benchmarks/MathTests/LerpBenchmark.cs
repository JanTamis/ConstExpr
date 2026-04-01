using BenchmarkDotNet.Attributes;

namespace ConstExpr.Benchmarks.MathTests;

/// <summary>
/// Compares scalar linear-interpolation implementations for float and double.
///
/// Candidates:
///   1. DotNetLerp        — Math.Lerp (built-in, branch-free, numerically stable)
///   2. FmaLerp           — FusedMultiplyAdd(t, b-a, a)  ← current ConstExpr output
///   3. NaiveLerp         — a + (b - a) * t              ← classic textbook formula
///   4. ComplementLerp    — a * (1 - t) + b * t          ← alternative stable formula
///
/// Run command:
///   dotnet run -c Release --project ConstExpr.Benchmarks/ConstExpr.Benchmarks.csproj --filter '*LerpBenchmark*'
/// </summary>
[MemoryDiagnoser]
public class LerpBenchmarkFloat
{
	private const int N = 1_024;

	private float[] _a = null!;
	private float[] _b = null!;
	private float[] _t = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_a = new float[N];
		_b = new float[N];
		_t = new float[N];
		for (var i = 0; i < N; i++)
		{
			_a[i] = (float)(rng.NextDouble() * 200 - 100);
			_b[i] = (float)(rng.NextDouble() * 200 - 100);
			_t[i] = (float)rng.NextDouble();
		}
	}

	/// <summary>float.Lerp — the .NET built-in baseline (numerically stable, uses FMA internally).</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	public float DotNetLerp()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += float.Lerp(_a[i], _b[i], _t[i]);
		return sum;
	}

	/// <summary>FMA: FusedMultiplyAdd(t, b-a, a) — current ConstExpr generator output.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public float FmaLerp()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += Single.FusedMultiplyAdd(_t[i], _b[i] - _a[i], _a[i]);
		return sum;
	}

	/// <summary>Naive: a + (b - a) * t — classic textbook formula, fewest operations.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public float NaiveLerp()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += _a[i] + (_b[i] - _a[i]) * _t[i];
		return sum;
	}

	/// <summary>Complement: a*(1-t) + b*t — two multiplies + add, numerically stable.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public float ComplementLerp()
	{
		var sum = 0f;
		for (var i = 0; i < N; i++)
			sum += _a[i] * (1f - _t[i]) + _b[i] * _t[i];
		return sum;
	}
}

[MemoryDiagnoser]
public class LerpBenchmarkDouble
{
	private const int N = 1_024;

	private double[] _a = null!;
	private double[] _b = null!;
	private double[] _t = null!;

	[GlobalSetup]
	public void Setup()
	{
		var rng = new Random(42);
		_a = new double[N];
		_b = new double[N];
		_t = new double[N];
		for (var i = 0; i < N; i++)
		{
			_a[i] = rng.NextDouble() * 200 - 100;
			_b[i] = rng.NextDouble() * 200 - 100;
			_t[i] = rng.NextDouble();
		}
	}

	/// <summary>double.Lerp — the .NET built-in baseline (uses FMA internally).</summary>
	[Benchmark(Baseline = true, OperationsPerInvoke = N)]
	public double DotNetLerp()
	{
		var sum = 0d;
		for (var i = 0; i < N; i++)
			sum += double.Lerp(_a[i], _b[i], _t[i]);
		return sum;
	}

	/// <summary>FMA: FusedMultiplyAdd(t, b-a, a) — current ConstExpr generator output.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public double FmaLerp()
	{
		var sum = 0d;
		for (var i = 0; i < N; i++)
			sum += Double.FusedMultiplyAdd(_t[i], _b[i] - _a[i], _a[i]);
		return sum;
	}

	/// <summary>Naive: a + (b - a) * t — classic textbook formula.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public double NaiveLerp()
	{
		var sum = 0d;
		for (var i = 0; i < N; i++)
			sum += _a[i] + (_b[i] - _a[i]) * _t[i];
		return sum;
	}

	/// <summary>Complement: a*(1-t) + b*t — two multiplies + add.</summary>
	[Benchmark(OperationsPerInvoke = N)]
	public double ComplementLerp()
	{
		var sum = 0d;
		for (var i = 0; i < N; i++)
			sum += _a[i] * (1d - _t[i]) + _b[i] * _t[i];
		return sum;
	}
}






