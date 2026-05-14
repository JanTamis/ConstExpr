```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method              | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetAsinPi_Double | Double     | 3.267 ns | 0.0293 ns | 0.0260 ns |  1.00 |    0.01 |         - |          NA |
| FastAsinPi_Double   | Double     | 1.001 ns | 0.0118 ns | 0.0111 ns |  0.31 |    0.00 |         - |          NA |
| FastAsinPiV2_Double | Double     | 1.104 ns | 0.0056 ns | 0.0049 ns |  0.34 |    0.00 |         - |          NA |
| FastAsinPiV3_Double | Double     | 1.108 ns | 0.0046 ns | 0.0039 ns |  0.34 |    0.00 |         - |          NA |
|                     |            |          |           |           |       |         |           |             |
| DotNetAsinPi_Float  | Float      | 2.499 ns | 0.0435 ns | 0.0407 ns |  1.00 |    0.02 |         - |          NA |
| FastAsinPi_Float    | Float      | 1.102 ns | 0.0130 ns | 0.0121 ns |  0.44 |    0.01 |         - |          NA |
| FastAsinPiV2_Float  | Float      | 1.116 ns | 0.0045 ns | 0.0039 ns |  0.45 |    0.01 |         - |          NA |
| FastAsinPiV3_Float  | Float      | 1.120 ns | 0.0035 ns | 0.0029 ns |  0.45 |    0.01 |         - |          NA |
