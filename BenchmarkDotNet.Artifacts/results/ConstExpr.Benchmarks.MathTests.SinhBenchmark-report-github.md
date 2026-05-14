```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method            | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------ |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetSinh_Double | Double     | 2.942 ns | 0.0049 ns | 0.0044 ns |  1.00 |         - |          NA |
| FastSinh_Double   | Double     | 2.182 ns | 0.0042 ns | 0.0037 ns |  0.74 |         - |          NA |
| FastSinhV2_Double | Double     | 2.119 ns | 0.0125 ns | 0.0111 ns |  0.72 |         - |          NA |
| FastSinhV3_Double | Double     | 6.105 ns | 0.0081 ns | 0.0076 ns |  2.07 |         - |          NA |
|                   |            |          |           |           |       |           |             |
| DotNetSinh_Float  | Float      | 2.139 ns | 0.0131 ns | 0.0116 ns |  1.00 |         - |          NA |
| FastSinh_Float    | Float      | 1.902 ns | 0.0038 ns | 0.0032 ns |  0.89 |         - |          NA |
| FastSinhV2_Float  | Float      | 1.764 ns | 0.0056 ns | 0.0050 ns |  0.82 |         - |          NA |
| FastSinhV3_Float  | Float      | 3.247 ns | 0.0039 ns | 0.0034 ns |  1.52 |         - |          NA |
