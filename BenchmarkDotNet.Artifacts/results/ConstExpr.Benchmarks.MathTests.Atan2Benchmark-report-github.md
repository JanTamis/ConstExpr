```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetAtan2_Double | Double     | 5.790 ns | 0.1005 ns | 0.0891 ns |  1.00 |    0.02 |         - |          NA |
| FastAtan2_Double   | Double     | 1.669 ns | 0.0093 ns | 0.0077 ns |  0.29 |    0.00 |         - |          NA |
| FastAtan2V2_Double | Double     | 2.370 ns | 0.0469 ns | 0.0416 ns |  0.41 |    0.01 |         - |          NA |
| FastAtan2V3_Double | Double     | 2.626 ns | 0.0101 ns | 0.0079 ns |  0.45 |    0.01 |         - |          NA |
|                    |            |          |           |           |       |         |           |             |
| DotNetAtan2_Float  | Float      | 2.684 ns | 0.0386 ns | 0.0361 ns |  1.00 |    0.02 |         - |          NA |
| FastAtan2_Float    | Float      | 1.687 ns | 0.0148 ns | 0.0131 ns |  0.63 |    0.01 |         - |          NA |
| FastAtan2V2_Float  | Float      | 2.446 ns | 0.0459 ns | 0.0429 ns |  0.91 |    0.02 |         - |          NA |
| FastAtan2V3_Float  | Float      | 2.241 ns | 0.0294 ns | 0.0246 ns |  0.84 |    0.01 |         - |          NA |
