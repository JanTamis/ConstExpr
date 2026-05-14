```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                     | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetSinCosPi_Double      | Double     | 4.095 ns | 0.0088 ns | 0.0078 ns |  1.00 |         - |          NA |
| CurrentFastSinCosPi_Double | Double     | 1.624 ns | 0.0241 ns | 0.0225 ns |  0.40 |         - |          NA |
| FastSinCosPiV2_Double      | Double     | 1.546 ns | 0.0054 ns | 0.0050 ns |  0.38 |         - |          NA |
|                            |            |          |           |           |       |           |             |
| DotNetSinCosPi_Float       | Float      | 3.432 ns | 0.0042 ns | 0.0037 ns |  1.00 |         - |          NA |
| CurrentFastSinCosPi_Float  | Float      | 1.549 ns | 0.0032 ns | 0.0028 ns |  0.45 |         - |          NA |
| FastSinCosPiV2_Float       | Float      | 1.443 ns | 0.0077 ns | 0.0072 ns |  0.42 |         - |          NA |
