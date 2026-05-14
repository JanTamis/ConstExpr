```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetExp10_Double | Double     | 4.866 ns | 0.0174 ns | 0.0145 ns |  1.00 |    0.00 |         - |          NA |
| FastExp10_Double   | Double     | 1.354 ns | 0.0068 ns | 0.0060 ns |  0.28 |    0.00 |         - |          NA |
| FastExp10V2_Double | Double     | 1.029 ns | 0.0068 ns | 0.0060 ns |  0.21 |    0.00 |         - |          NA |
| FastExp10V3_Double | Double     | 1.080 ns | 0.0018 ns | 0.0016 ns |  0.22 |    0.00 |         - |          NA |
|                    |            |          |           |           |       |         |           |             |
| DotNetExp10_Float  | Float      | 2.525 ns | 0.0387 ns | 0.0343 ns |  1.00 |    0.02 |         - |          NA |
| FastExp10_Float    | Float      | 1.391 ns | 0.0010 ns | 0.0008 ns |  0.55 |    0.01 |         - |          NA |
| FastExp10V2_Float  | Float      | 1.030 ns | 0.0065 ns | 0.0054 ns |  0.41 |    0.01 |         - |          NA |
| FastExp10V3_Float  | Float      | 1.078 ns | 0.0012 ns | 0.0010 ns |  0.43 |    0.01 |         - |          NA |
