```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                  | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetAcosh_Double      | Double     | 3.407 ns | 1.2254 ns | 0.0672 ns |  1.00 |    0.02 |         - |          NA |
| FastAcosh_Double        | Double     | 1.804 ns | 0.0427 ns | 0.0023 ns |  0.53 |    0.01 |         - |          NA |
| FastAcosh_Double_Fixed  | Double     | 1.698 ns | 0.0945 ns | 0.0052 ns |  0.50 |    0.01 |         - |          NA |
| FastAcosh_Double_Simple | Double     | 3.002 ns | 0.1115 ns | 0.0061 ns |  0.88 |    0.02 |         - |          NA |
|                         |            |          |           |           |       |         |           |             |
| DotNetAcosh_Float       | Float      | 2.353 ns | 0.1682 ns | 0.0092 ns |  1.00 |    0.00 |         - |          NA |
| FastAcosh_Float         | Float      | 1.802 ns | 0.0477 ns | 0.0026 ns |  0.77 |    0.00 |         - |          NA |
| FastAcosh_Float_Fixed   | Float      | 1.713 ns | 0.5916 ns | 0.0324 ns |  0.73 |    0.01 |         - |          NA |
| FastAcosh_Float_Simple  | Float      | 1.999 ns | 0.0206 ns | 0.0011 ns |  0.85 |    0.00 |         - |          NA |
