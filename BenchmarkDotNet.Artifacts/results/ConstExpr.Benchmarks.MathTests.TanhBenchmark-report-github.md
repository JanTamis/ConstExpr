```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                 | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetTanh_Double      | Double     | 2.624 ns | 0.0033 ns | 0.0029 ns |  1.00 |         - |          NA |
| CurrentFastTanh_Double | Double     | 2.601 ns | 0.0030 ns | 0.0028 ns |  0.99 |         - |          NA |
| FastTanhV2_Double      | Double     | 1.761 ns | 0.0014 ns | 0.0011 ns |  0.67 |         - |          NA |
| FastTanhV3_Double      | Double     | 1.587 ns | 0.0013 ns | 0.0012 ns |  0.60 |         - |          NA |
|                        |            |          |           |           |       |           |             |
| DotNetTanh_Float       | Float      | 2.061 ns | 0.0036 ns | 0.0033 ns |  1.00 |         - |          NA |
| CurrentFastTanh_Float  | Float      | 1.868 ns | 0.0027 ns | 0.0026 ns |  0.91 |         - |          NA |
| FastTanhV2_Float       | Float      | 1.597 ns | 0.0024 ns | 0.0019 ns |  0.77 |         - |          NA |
| FastTanhV3_Float       | Float      | 1.576 ns | 0.0050 ns | 0.0042 ns |  0.76 |         - |          NA |
