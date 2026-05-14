```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean     | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------- |----------- |---------:|----------:|----------:|------:|----------:|------------:|
| DotNetAsinh_Double | Double     | 4.118 ns | 0.0100 ns | 0.0083 ns |  1.00 |         - |          NA |
| FastAsinh_Double   | Double     | 2.847 ns | 0.0033 ns | 0.0031 ns |  0.69 |         - |          NA |
| FastAsinhV2_Double | Double     | 2.732 ns | 0.0019 ns | 0.0017 ns |  0.66 |         - |          NA |
| FastAsinhV3_Double | Double     | 2.758 ns | 0.0040 ns | 0.0032 ns |  0.67 |         - |          NA |
|                    |            |          |           |           |       |           |             |
| DotNetAsinh_Float  | Float      | 2.267 ns | 0.0213 ns | 0.0199 ns |  1.00 |         - |          NA |
| FastAsinh_Float    | Float      | 2.289 ns | 0.0024 ns | 0.0020 ns |  1.01 |         - |          NA |
| FastAsinhV2_Float  | Float      | 1.999 ns | 0.0026 ns | 0.0023 ns |  0.88 |         - |          NA |
| FastAsinhV3_Float  | Float      | 2.502 ns | 0.0036 ns | 0.0030 ns |  1.10 |         - |          NA |
