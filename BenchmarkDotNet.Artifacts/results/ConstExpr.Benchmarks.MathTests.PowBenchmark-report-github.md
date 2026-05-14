```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method           | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetPow_Double | Double     | 4.943 ns | 0.0071 ns | 0.0059 ns |  1.00 |         - |          NA |
| FastPow_Double   | Double     | 2.987 ns | 0.0052 ns | 0.0049 ns |  0.60 |         - |          NA |
| FastPowV2_Double | Double     | 2.965 ns | 0.0028 ns | 0.0027 ns |  0.60 |         - |          NA |
| FastPowV3_Double | Double     | 3.129 ns | 0.0074 ns | 0.0062 ns |  0.63 |         - |          NA |
|                  |            |          |           |           |       |           |             |
| DotNetPow_Float  | Float      | 2.508 ns | 0.0078 ns | 0.0061 ns |  1.00 |         - |          NA |
| FastPow_Float    | Float      | 3.001 ns | 0.0041 ns | 0.0035 ns |  1.20 |         - |          NA |
| FastPowV2_Float  | Float      | 2.707 ns | 0.0068 ns | 0.0057 ns |  1.08 |         - |          NA |
| FastPowV3_Float  | Float      | 3.001 ns | 0.0021 ns | 0.0016 ns |  1.20 |         - |          NA |
