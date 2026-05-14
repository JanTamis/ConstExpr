```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                    | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetBitDecrement_Double | Double     | 0.9882 ns | 0.0123 ns | 0.0116 ns |  1.00 |    0.02 |         - |          NA |
| FastBitDecrementV2_Double | Double     | 0.6520 ns | 0.0112 ns | 0.0105 ns |  0.66 |    0.01 |         - |          NA |
| FastBitDecrementV3_Double | Double     | 0.6445 ns | 0.0039 ns | 0.0035 ns |  0.65 |    0.01 |         - |          NA |
|                           |            |           |           |           |       |         |           |             |
| DotNetBitDecrement_Float  | Float      | 0.9963 ns | 0.0116 ns | 0.0108 ns |  1.00 |    0.01 |         - |          NA |
| FastBitDecrementV2_Float  | Float      | 0.6486 ns | 0.0014 ns | 0.0012 ns |  0.65 |    0.01 |         - |          NA |
| FastBitDecrementV3_Float  | Float      | 0.6582 ns | 0.0075 ns | 0.0067 ns |  0.66 |    0.01 |         - |          NA |
