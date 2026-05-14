```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                 | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetAtan_Double      | Double     | 3.420 ns | 0.0264 ns | 0.0220 ns |  1.00 |    0.01 |         - |          NA |
| DotNetAtanPi_Double    | Double     | 3.675 ns | 0.0109 ns | 0.0091 ns |  1.07 |    0.01 |         - |          NA |
| CurrentFastAtan_Double | Double     | 1.423 ns | 0.0034 ns | 0.0027 ns |  0.42 |    0.00 |         - |          NA |
| FastAtanV2_Double      | Double     | 1.224 ns | 0.0245 ns | 0.0282 ns |  0.36 |    0.01 |         - |          NA |
| FastAtanV3_Double      | Double     | 2.008 ns | 0.0335 ns | 0.0344 ns |  0.59 |    0.01 |         - |          NA |
|                        |            |          |           |           |       |         |           |             |
| DotNetAtan_Float       | Float      | 3.375 ns | 0.0564 ns | 0.0528 ns |  1.00 |    0.02 |         - |          NA |
| DotNetAtanPi_Float     | Float      | 3.681 ns | 0.0652 ns | 0.0610 ns |  1.09 |    0.02 |         - |          NA |
| CurrentFastAtan_Float  | Float      | 1.461 ns | 0.0165 ns | 0.0138 ns |  0.43 |    0.01 |         - |          NA |
| FastAtanV2_Float       | Float      | 1.200 ns | 0.0240 ns | 0.0320 ns |  0.36 |    0.01 |         - |          NA |
| FastAtanV3_Float       | Float      | 1.197 ns | 0.0235 ns | 0.0345 ns |  0.35 |    0.01 |         - |          NA |
