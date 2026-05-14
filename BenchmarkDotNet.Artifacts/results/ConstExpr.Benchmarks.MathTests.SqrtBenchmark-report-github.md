```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                  | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SqrtOfSquared_Float     | AlgOpt     | 0.4973 ns | 0.0026 ns | 0.0022 ns |  1.00 |    0.01 |         - |          NA |
| AbsOptimized_Float      | AlgOpt     | 0.5363 ns | 0.0069 ns | 0.0062 ns |  1.08 |    0.01 |         - |          NA |
|                         |            |           |           |           |       |         |           |             |
| DotNet_Double           | Double     | 0.4768 ns | 0.0050 ns | 0.0054 ns |  1.00 |    0.02 |         - |          NA |
| BitHackNewton3_Double   | Double     | 2.0607 ns | 0.0205 ns | 0.0192 ns |  4.32 |    0.06 |         - |          NA |
| FloatSqrtNewton2_Double | Double     | 1.5426 ns | 0.0155 ns | 0.0145 ns |  3.24 |    0.05 |         - |          NA |
|                         |            |           |           |           |       |         |           |             |
| DotNet_Float            | Float      | 0.4856 ns | 0.0059 ns | 0.0052 ns |  1.00 |    0.01 |         - |          NA |
| BitHackNewton2_Float    | Float      | 1.2415 ns | 0.0086 ns | 0.0067 ns |  2.56 |    0.03 |         - |          NA |
| RsqrtNewton_Float       | Float      | 1.0071 ns | 0.0200 ns | 0.0197 ns |  2.07 |    0.04 |         - |          NA |
