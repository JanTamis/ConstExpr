```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                  | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------------ |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetTanPi_Double      | Double     | 3.410 ns | 0.0029 ns | 0.0022 ns |  1.00 |         - |          NA |
| CurrentFastTanPi_Double | Double     | 1.513 ns | 0.0011 ns | 0.0008 ns |  0.44 |         - |          NA |
| FastTanPiV2_Double      | Double     | 1.248 ns | 0.0022 ns | 0.0021 ns |  0.37 |         - |          NA |
| FastTanPiV3_Double      | Double     | 1.227 ns | 0.0012 ns | 0.0011 ns |  0.36 |         - |          NA |
|                         |            |          |           |           |       |           |             |
| DotNetTanPi_Float       | Float      | 2.477 ns | 0.0039 ns | 0.0036 ns |  1.00 |         - |          NA |
| CurrentFastTanPi_Float  | Float      | 1.266 ns | 0.0022 ns | 0.0018 ns |  0.51 |         - |          NA |
| FastTanPiV2_Float       | Float      | 1.178 ns | 0.0029 ns | 0.0023 ns |  0.48 |         - |          NA |
| FastTanPiV3_Float       | Float      | 1.196 ns | 0.0041 ns | 0.0034 ns |  0.48 |         - |          NA |
