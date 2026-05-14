```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                   | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetSinCos_Double      | Double     | 5.327 ns | 0.0245 ns | 0.0205 ns |  1.00 |         - |          NA |
| CurrentFastSinCos_Double | Double     | 1.839 ns | 0.0090 ns | 0.0070 ns |  0.35 |         - |          NA |
| FastSinCosV2_Double      | Double     | 1.622 ns | 0.0049 ns | 0.0041 ns |  0.30 |         - |          NA |
|                          |            |          |           |           |       |           |             |
| DotNetSinCos_Float       | Float      | 3.084 ns | 0.0167 ns | 0.0139 ns |  1.00 |         - |          NA |
| CurrentFastSinCos_Float  | Float      | 1.782 ns | 0.0060 ns | 0.0054 ns |  0.58 |         - |          NA |
| FastSinCosV2_Float       | Float      | 1.597 ns | 0.0025 ns | 0.0021 ns |  0.52 |         - |          NA |
