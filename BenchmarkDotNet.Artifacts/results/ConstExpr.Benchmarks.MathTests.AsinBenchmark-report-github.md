```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method            | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------ |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetAsin_Double | Double     | 3.0130 ns | 0.0146 ns | 0.0130 ns |  1.00 |         - |          NA |
| FastAsin_Double   | Double     | 0.9390 ns | 0.0017 ns | 0.0013 ns |  0.31 |         - |          NA |
| FastAsinV2_Double | Double     | 1.9412 ns | 0.0231 ns | 0.0216 ns |  0.64 |         - |          NA |
| FastAsinV3_Double | Double     | 1.0167 ns | 0.0052 ns | 0.0040 ns |  0.34 |         - |          NA |
|                   |            |           |           |           |       |           |             |
| DotNetAsin_Float  | Float      | 2.2519 ns | 0.0103 ns | 0.0086 ns |  1.00 |         - |          NA |
| FastAsin_Float    | Float      | 1.0025 ns | 0.0010 ns | 0.0009 ns |  0.45 |         - |          NA |
| FastAsinV2_Float  | Float      | 1.0124 ns | 0.0082 ns | 0.0077 ns |  0.45 |         - |          NA |
