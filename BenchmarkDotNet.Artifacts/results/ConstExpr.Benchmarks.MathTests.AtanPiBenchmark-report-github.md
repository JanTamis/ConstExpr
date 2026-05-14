```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                   | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetAtanPi_Double      | Double     | 3.634 ns | 0.1067 ns | 0.0059 ns |  1.00 |    0.00 |         - |          NA |
| CurrentFastAtanPi_Double | Double     | 1.387 ns | 0.0323 ns | 0.0018 ns |  0.38 |    0.00 |         - |          NA |
| FastAtanPiV2_Double      | Double     | 1.217 ns | 0.0435 ns | 0.0024 ns |  0.33 |    0.00 |         - |          NA |
| FastAtanPiV3_Double      | Double     | 2.001 ns | 0.0945 ns | 0.0052 ns |  0.55 |    0.00 |         - |          NA |
|                          |            |          |           |           |       |         |           |             |
| DotNetAtanPi_Float       | Float      | 3.568 ns | 1.4182 ns | 0.0777 ns |  1.00 |    0.03 |         - |          NA |
| CurrentFastAtanPi_Float  | Float      | 1.543 ns | 0.0440 ns | 0.0024 ns |  0.43 |    0.01 |         - |          NA |
| FastAtanPiV2_Float       | Float      | 1.243 ns | 0.3365 ns | 0.0184 ns |  0.35 |    0.01 |         - |          NA |
| FastAtanPiV3_Float       | Float      | 1.179 ns | 0.2128 ns | 0.0117 ns |  0.33 |    0.01 |         - |          NA |
