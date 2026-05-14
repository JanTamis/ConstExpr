```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                    | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|-------------------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetAtan2Pi_Double      | Double     | 6.478 ns | 0.1832 ns | 0.0100 ns |  1.00 |         - |          NA |
| CurrentFastAtan2Pi_Double | Double     | 1.657 ns | 0.2032 ns | 0.0111 ns |  0.26 |         - |          NA |
| FastAtan2PiV2_Double      | Double     | 2.284 ns | 0.1684 ns | 0.0092 ns |  0.35 |         - |          NA |
| FastAtan2PiV3_Double      | Double     | 2.626 ns | 0.2004 ns | 0.0110 ns |  0.41 |         - |          NA |
|                           |            |          |           |           |       |           |             |
| DotNetAtan2Pi_Float       | Float      | 3.145 ns | 0.2656 ns | 0.0146 ns |  1.00 |         - |          NA |
| CurrentFastAtan2Pi_Float  | Float      | 1.685 ns | 0.4323 ns | 0.0237 ns |  0.54 |         - |          NA |
| FastAtan2PiV2_Float       | Float      | 2.250 ns | 0.9124 ns | 0.0500 ns |  0.72 |         - |          NA |
| FastAtan2PiV3_Float       | Float      | 2.142 ns | 0.2489 ns | 0.0136 ns |  0.68 |         - |          NA |
