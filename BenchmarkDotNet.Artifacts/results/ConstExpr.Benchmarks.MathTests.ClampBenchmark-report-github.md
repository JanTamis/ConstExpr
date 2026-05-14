```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method           | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetClamp      | 0.6514 ns | 0.0130 ns | 0.0144 ns |  1.00 |    0.03 |         - |          NA |
| MinMaxClamp      | 0.4931 ns | 0.0098 ns | 0.0144 ns |  0.76 |    0.03 |         - |          NA |
| TernaryClamp     | 0.6569 ns | 0.0112 ns | 0.0104 ns |  1.01 |    0.03 |         - |          NA |
| BranchlessClamp  | 0.5053 ns | 0.0049 ns | 0.0043 ns |  0.78 |    0.02 |         - |          NA |
| GenericMathClamp | 0.6783 ns | 0.0133 ns | 0.0148 ns |  1.04 |    0.03 |         - |          NA |
