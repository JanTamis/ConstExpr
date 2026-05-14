```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method            | Categories | Root | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |----------- |----- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **DotNet_Double**     | **Double**     | **5**    |  **5.566 ns** | **0.1165 ns** | **0.0064 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Current_Double    | Double     | 5    |  7.119 ns | 2.3093 ns | 0.1266 ns |  1.28 |    0.02 |         - |          NA |
| V2_Double         | Double     | 5    |  4.982 ns | 0.2240 ns | 0.0123 ns |  0.89 |    0.00 |         - |          NA |
| ExpLog_Double     | Double     | 5    |  4.851 ns | 0.6782 ns | 0.0372 ns |  0.87 |    0.01 |         - |          NA |
| FastExpLog_Double | Double     | 5    |  2.311 ns | 0.0757 ns | 0.0041 ns |  0.42 |    0.00 |         - |          NA |
|                   |            |      |           |           |           |       |         |           |             |
| **DotNet_Double**     | **Double**     | **7**    |  **5.571 ns** | **0.0520 ns** | **0.0029 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Current_Double    | Double     | 7    |  6.441 ns | 0.2300 ns | 0.0126 ns |  1.16 |    0.00 |         - |          NA |
| V2_Double         | Double     | 7    |  4.956 ns | 0.1913 ns | 0.0105 ns |  0.89 |    0.00 |         - |          NA |
| ExpLog_Double     | Double     | 7    |  4.750 ns | 0.3775 ns | 0.0207 ns |  0.85 |    0.00 |         - |          NA |
| FastExpLog_Double | Double     | 7    |  2.295 ns | 0.0112 ns | 0.0006 ns |  0.41 |    0.00 |         - |          NA |
|                   |            |      |           |           |           |       |         |           |             |
| **DotNet_Double**     | **Double**     | **10**   |  **5.574 ns** | **0.0294 ns** | **0.0016 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Current_Double    | Double     | 10   | 10.388 ns | 1.6786 ns | 0.0920 ns |  1.86 |    0.01 |         - |          NA |
| V2_Double         | Double     | 10   |  6.267 ns | 0.1354 ns | 0.0074 ns |  1.12 |    0.00 |         - |          NA |
| ExpLog_Double     | Double     | 10   |  4.743 ns | 0.2019 ns | 0.0111 ns |  0.85 |    0.00 |         - |          NA |
| FastExpLog_Double | Double     | 10   |  2.296 ns | 0.0234 ns | 0.0013 ns |  0.41 |    0.00 |         - |          NA |
|                   |            |      |           |           |           |       |         |           |             |
| **DotNet_Float**      | **Float**      | **5**    |  **5.987 ns** | **1.7868 ns** | **0.0979 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Current_Float     | Float      | 5    |  4.236 ns | 0.1229 ns | 0.0067 ns |  0.71 |    0.01 |         - |          NA |
| V2_Float          | Float      | 5    |  4.950 ns | 0.2252 ns | 0.0123 ns |  0.83 |    0.01 |         - |          NA |
| ExpLog_Float      | Float      | 5    |  3.005 ns | 0.3481 ns | 0.0191 ns |  0.50 |    0.01 |         - |          NA |
| FastExpLog_Float  | Float      | 5    |  2.261 ns | 0.6480 ns | 0.0355 ns |  0.38 |    0.01 |         - |          NA |
|                   |            |      |           |           |           |       |         |           |             |
| **DotNet_Float**      | **Float**      | **7**    |  **5.878 ns** | **0.0380 ns** | **0.0021 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Current_Float     | Float      | 7    |  5.908 ns | 0.0907 ns | 0.0050 ns |  1.01 |    0.00 |         - |          NA |
| V2_Float          | Float      | 7    |  4.799 ns | 0.0206 ns | 0.0011 ns |  0.82 |    0.00 |         - |          NA |
| ExpLog_Float      | Float      | 7    |  2.992 ns | 0.0248 ns | 0.0014 ns |  0.51 |    0.00 |         - |          NA |
| FastExpLog_Float  | Float      | 7    |  2.220 ns | 0.0236 ns | 0.0013 ns |  0.38 |    0.00 |         - |          NA |
|                   |            |      |           |           |           |       |         |           |             |
| **DotNet_Float**      | **Float**      | **10**   |  **5.879 ns** | **0.1678 ns** | **0.0092 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Current_Float     | Float      | 10   |  8.933 ns | 3.7061 ns | 0.2031 ns |  1.52 |    0.03 |         - |          NA |
| V2_Float          | Float      | 10   |  6.112 ns | 0.0352 ns | 0.0019 ns |  1.04 |    0.00 |         - |          NA |
| ExpLog_Float      | Float      | 10   |  2.994 ns | 0.0264 ns | 0.0014 ns |  0.51 |    0.00 |         - |          NA |
| FastExpLog_Float  | Float      | 10   |  2.220 ns | 0.0220 ns | 0.0012 ns |  0.38 |    0.00 |         - |          NA |
