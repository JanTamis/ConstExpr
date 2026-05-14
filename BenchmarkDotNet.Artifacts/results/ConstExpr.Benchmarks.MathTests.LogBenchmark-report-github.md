```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                 | Categories      | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------------- |---------------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetLog2Param_Double | TwoParam_Double | 4.250 ns | 0.0066 ns | 0.0059 ns |  1.00 |         - |          NA |
| FastLog2Param_Double   | TwoParam_Double | 2.000 ns | 0.0017 ns | 0.0015 ns |  0.47 |         - |          NA |
|                        |                 |          |           |           |       |           |             |
| DotNetLog2Param_Float  | TwoParam_Float  | 4.541 ns | 0.0136 ns | 0.0106 ns |  1.00 |         - |          NA |
| FastLog2Param_Float    | TwoParam_Float  | 2.021 ns | 0.0234 ns | 0.0195 ns |  0.45 |         - |          NA |
