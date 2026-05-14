```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                  | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetSinPi_Double      | Double     | 2.637 ns | 0.0064 ns | 0.0053 ns |  1.00 |    0.00 |         - |          NA |
| CurrentFastSinPi_Double | Double     | 1.522 ns | 0.0139 ns | 0.0130 ns |  0.58 |    0.00 |         - |          NA |
| FastSinPiV2_Double      | Double     | 1.308 ns | 0.0058 ns | 0.0048 ns |  0.50 |    0.00 |         - |          NA |
| FastSinPiV3_Double      | Double     | 1.227 ns | 0.0020 ns | 0.0016 ns |  0.47 |    0.00 |         - |          NA |
|                         |            |          |           |           |       |         |           |             |
| DotNetSinPi_Float       | Float      | 2.432 ns | 0.0385 ns | 0.0360 ns |  1.00 |    0.02 |         - |          NA |
| CurrentFastSinPi_Float  | Float      | 1.503 ns | 0.0076 ns | 0.0060 ns |  0.62 |    0.01 |         - |          NA |
| FastSinPiV2_Float       | Float      | 1.225 ns | 0.0024 ns | 0.0019 ns |  0.50 |    0.01 |         - |          NA |
| FastSinPiV3_Float       | Float      | 1.126 ns | 0.0039 ns | 0.0030 ns |  0.46 |    0.01 |         - |          NA |
