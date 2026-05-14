```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method           | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetTan_Double | Double     | 2.799 ns | 0.0020 ns | 0.0018 ns |  1.00 |         - |          NA |
| FastTan_Double   | Double     | 1.493 ns | 0.0018 ns | 0.0015 ns |  0.53 |         - |          NA |
| FastTanV2_Double | Double     | 1.259 ns | 0.0202 ns | 0.0189 ns |  0.45 |         - |          NA |
| FastTanV3_Double | Double     | 1.464 ns | 0.0029 ns | 0.0023 ns |  0.52 |         - |          NA |
|                  |            |          |           |           |       |           |             |
| DotNetTan_Float  | Float      | 2.655 ns | 0.0231 ns | 0.0205 ns |  1.00 |         - |          NA |
| FastTan_Float    | Float      | 1.237 ns | 0.0059 ns | 0.0052 ns |  0.47 |         - |          NA |
| FastTanV2_Float  | Float      | 1.206 ns | 0.0032 ns | 0.0028 ns |  0.45 |         - |          NA |
| FastTanV3_Float  | Float      | 1.170 ns | 0.0027 ns | 0.0023 ns |  0.44 |         - |          NA |
