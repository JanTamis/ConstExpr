```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                        | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetDegreesToRadians_Double | Double     | 0.5358 ns | 0.0043 ns | 0.0038 ns |  1.00 |    0.01 |         - |          NA |
| CurrentOptimizer_Double       | Double     | 0.5863 ns | 0.0068 ns | 0.0064 ns |  1.09 |    0.01 |         - |          NA |
| FmaVariant_Double             | Double     | 0.5905 ns | 0.0050 ns | 0.0039 ns |  1.10 |    0.01 |         - |          NA |
|                               |            |           |           |           |       |         |           |             |
| DotNetDegreesToRadians_Float  | Float      | 0.5377 ns | 0.0072 ns | 0.0060 ns |  1.00 |    0.02 |         - |          NA |
| CurrentOptimizer_Float        | Float      | 0.5929 ns | 0.0065 ns | 0.0061 ns |  1.10 |    0.02 |         - |          NA |
| FmaVariant_Float              | Float      | 0.5880 ns | 0.0055 ns | 0.0046 ns |  1.09 |    0.01 |         - |          NA |
| DoubleIntermediary_Float      | Float      | 0.5997 ns | 0.0027 ns | 0.0021 ns |  1.12 |    0.01 |         - |          NA |
