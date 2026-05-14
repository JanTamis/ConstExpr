```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method            | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetCosh_Double | Double     | 2.880 ns | 0.0182 ns | 0.0010 ns |  1.00 |    0.00 |         - |          NA |
| FastCosh_Double   | Double     | 2.043 ns | 0.2057 ns | 0.0113 ns |  0.71 |    0.00 |         - |          NA |
| FastCoshV2_Double | Double     | 2.017 ns | 0.3926 ns | 0.0215 ns |  0.70 |    0.01 |         - |          NA |
| FastCoshV3_Double | Double     | 4.491 ns | 1.6177 ns | 0.0887 ns |  1.56 |    0.03 |         - |          NA |
|                   |            |          |           |           |       |         |           |             |
| DotNetCosh_Float  | Float      | 2.092 ns | 0.1346 ns | 0.0074 ns |  1.00 |    0.00 |         - |          NA |
| FastCosh_Float    | Float      | 1.751 ns | 0.0904 ns | 0.0050 ns |  0.84 |    0.00 |         - |          NA |
| FastCoshV2_Float  | Float      | 1.766 ns | 0.2294 ns | 0.0126 ns |  0.84 |    0.01 |         - |          NA |
| FastCoshV3_Float  | Float      | 3.257 ns | 0.0910 ns | 0.0050 ns |  1.56 |    0.01 |         - |          NA |
