```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                    | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetBitIncrement_Double | Double     | 0.8072 ns | 0.0152 ns | 0.0127 ns |  1.00 |    0.02 |         - |          NA |
| FastBitIncrementV2_Double | Double     | 0.6519 ns | 0.0084 ns | 0.0079 ns |  0.81 |    0.02 |         - |          NA |
| FastBitIncrementV3_Double | Double     | 0.6467 ns | 0.0050 ns | 0.0044 ns |  0.80 |    0.01 |         - |          NA |
|                           |            |           |           |           |       |         |           |             |
| DotNetBitIncrement_Float  | Float      | 0.8165 ns | 0.0163 ns | 0.0206 ns |  1.00 |    0.03 |         - |          NA |
| FastBitIncrementV2_Float  | Float      | 0.6544 ns | 0.0103 ns | 0.0097 ns |  0.80 |    0.02 |         - |          NA |
| FastBitIncrementV3_Float  | Float      | 0.6561 ns | 0.0080 ns | 0.0075 ns |  0.80 |    0.02 |         - |          NA |
