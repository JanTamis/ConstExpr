```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetLog10_Double | Double     | 2.0197 ns | 0.0030 ns | 0.0028 ns |  1.00 |    0.00 |         - |          NA |
| FastLog10_Double   | Double     | 0.9431 ns | 0.0016 ns | 0.0015 ns |  0.47 |    0.00 |         - |          NA |
| FastLog10V2_Double | Double     | 0.8920 ns | 0.0022 ns | 0.0019 ns |  0.44 |    0.00 |         - |          NA |
| FastLog10V3_Double | Double     | 2.0216 ns | 0.0047 ns | 0.0039 ns |  1.00 |    0.00 |         - |          NA |
|                    |            |           |           |           |       |         |           |             |
| DotNetLog10_Float  | Float      | 1.7819 ns | 0.0232 ns | 0.0217 ns |  1.00 |    0.02 |         - |          NA |
| FastLog10_Float    | Float      | 0.9498 ns | 0.0059 ns | 0.0049 ns |  0.53 |    0.01 |         - |          NA |
| FastLog10V2_Float  | Float      | 0.8975 ns | 0.0039 ns | 0.0030 ns |  0.50 |    0.01 |         - |          NA |
| FastLog10V3_Float  | Float      | 1.5111 ns | 0.0065 ns | 0.0050 ns |  0.85 |    0.01 |         - |          NA |
