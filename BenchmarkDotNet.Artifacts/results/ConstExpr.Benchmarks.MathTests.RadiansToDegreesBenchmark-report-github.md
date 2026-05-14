```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                        | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetRadiansToDegrees_Double | Double     | 0.4951 ns | 0.0025 ns | 0.0021 ns |  1.00 |    0.01 |         - |          NA |
| CurrentOptimizer_Double       | Double     | 0.5350 ns | 0.0023 ns | 0.0022 ns |  1.08 |    0.01 |         - |          NA |
| FmaVariant_Double             | Double     | 0.5415 ns | 0.0032 ns | 0.0025 ns |  1.09 |    0.01 |         - |          NA |
|                               |            |           |           |           |       |         |           |             |
| DotNetRadiansToDegrees_Float  | Float      | 0.5076 ns | 0.0021 ns | 0.0020 ns |  1.00 |    0.01 |         - |          NA |
| CurrentOptimizer_Float        | Float      | 0.5435 ns | 0.0041 ns | 0.0035 ns |  1.07 |    0.01 |         - |          NA |
| FmaVariant_Float              | Float      | 0.5465 ns | 0.0031 ns | 0.0024 ns |  1.08 |    0.01 |         - |          NA |
| DoubleIntermediary_Float      | Float      | 0.5803 ns | 0.0109 ns | 0.0102 ns |  1.14 |    0.02 |         - |          NA |
