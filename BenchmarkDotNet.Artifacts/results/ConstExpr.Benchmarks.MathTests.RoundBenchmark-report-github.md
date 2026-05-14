```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method               | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetRound_Double   | Double     | 0.5296 ns | 0.0027 ns | 0.0023 ns |  1.00 |    0.01 |         - |          NA |
| GenericMath_Double   | Double     | 0.5471 ns | 0.0104 ns | 0.0138 ns |  1.03 |    0.03 |         - |          NA |
| AwayFromZero_Double  | Double     | 0.5555 ns | 0.0049 ns | 0.0041 ns |  1.05 |    0.01 |         - |          NA |
| FloorPlusHalf_Double | Double     | 0.5891 ns | 0.0032 ns | 0.0030 ns |  1.11 |    0.01 |         - |          NA |
| LongCast_Double      | Double     | 0.6722 ns | 0.0019 ns | 0.0017 ns |  1.27 |    0.01 |         - |          NA |
|                      |            |           |           |           |       |         |           |             |
| DotNetRound_Float    | Float      | 0.5786 ns | 0.0051 ns | 0.0042 ns |  1.00 |    0.01 |         - |          NA |
| GenericMath_Float    | Float      | 0.5814 ns | 0.0034 ns | 0.0030 ns |  1.00 |    0.01 |         - |          NA |
| AwayFromZero_Float   | Float      | 0.5814 ns | 0.0025 ns | 0.0023 ns |  1.00 |    0.01 |         - |          NA |
| FloorPlusHalf_Float  | Float      | 0.6029 ns | 0.0019 ns | 0.0015 ns |  1.04 |    0.01 |         - |          NA |
| IntCast_Float        | Float      | 0.6765 ns | 0.0012 ns | 0.0010 ns |  1.17 |    0.01 |         - |          NA |
|                      |            |           |           |           |       |         |           |             |
| UnaryMinus_Direct    | UnaryMinus | 0.5941 ns | 0.0023 ns | 0.0022 ns |  1.00 |    0.01 |         - |          NA |
| UnaryMinus_Rewritten | UnaryMinus | 0.5867 ns | 0.0031 ns | 0.0029 ns |  0.99 |    0.01 |         - |          NA |
