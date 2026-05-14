```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                  | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------------ |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetAtanh_Double      | Double     | 4.913 ns | 0.0480 ns | 0.0449 ns |  1.00 |         - |          NA |
| CurrentFastAtanh_Double | Double     | 1.869 ns | 0.0041 ns | 0.0036 ns |  0.38 |         - |          NA |
| FastAtanhV2_Double      | Double     | 3.024 ns | 0.0158 ns | 0.0148 ns |  0.62 |         - |          NA |
| FastAtanhV3_Double      | Double     | 2.455 ns | 0.0085 ns | 0.0080 ns |  0.50 |         - |          NA |
|                         |            |          |           |           |       |           |             |
| DotNetAtanh_Float       | Float      | 2.270 ns | 0.0065 ns | 0.0060 ns |  1.00 |         - |          NA |
| CurrentFastAtanh_Float  | Float      | 1.720 ns | 0.0075 ns | 0.0070 ns |  0.76 |         - |          NA |
| FastAtanhV2_Float       | Float      | 1.995 ns | 0.0018 ns | 0.0016 ns |  0.88 |         - |          NA |
| FastAtanhV3_Float       | Float      | 1.746 ns | 0.0016 ns | 0.0015 ns |  0.77 |         - |          NA |
