```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|---------------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetScaleB_Double   | Double     | 0.6551 ns | 0.0009 ns | 0.0007 ns |  1.00 |         - |          NA |
| CurrentScaleB_Double  | Double     | 0.7243 ns | 0.0009 ns | 0.0007 ns |  1.11 |         - |          NA |
| UnsafeScaleB_Double   | Double     | 0.7258 ns | 0.0021 ns | 0.0019 ns |  1.11 |         - |          NA |
| FastPathScaleB_Double | Double     | 0.6657 ns | 0.0009 ns | 0.0008 ns |  1.02 |         - |          NA |
|                       |            |           |           |           |       |           |             |
| DotNetScaleB_Float    | Float      | 0.6576 ns | 0.0005 ns | 0.0004 ns |  1.00 |         - |          NA |
| CurrentScaleB_Float   | Float      | 0.7035 ns | 0.0049 ns | 0.0044 ns |  1.07 |         - |          NA |
| UnsafeScaleB_Float    | Float      | 0.7018 ns | 0.0024 ns | 0.0021 ns |  1.07 |         - |          NA |
| FastPathScaleB_Float  | Float      | 0.6553 ns | 0.0003 ns | 0.0002 ns |  1.00 |         - |          NA |
