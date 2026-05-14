```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method           | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetSin_Double | Double     | 2.9285 ns | 0.0096 ns | 0.0080 ns |  1.00 |         - |          NA |
| FastSin_Double   | Double     | 1.0923 ns | 0.0014 ns | 0.0013 ns |  0.37 |         - |          NA |
| FastSinV2_Double | Double     | 1.1197 ns | 0.0008 ns | 0.0007 ns |  0.38 |         - |          NA |
|                  |            |           |           |           |       |           |             |
| DotNetSin_Float  | Float      | 2.3440 ns | 0.0063 ns | 0.0052 ns |  1.00 |         - |          NA |
| FastSin_Float    | Float      | 0.9377 ns | 0.0008 ns | 0.0007 ns |  0.40 |         - |          NA |
| FastSinV2_Float  | Float      | 0.9566 ns | 0.0012 ns | 0.0011 ns |  0.41 |         - |          NA |
| FastSinV3_Float  | Float      | 0.8877 ns | 0.0006 ns | 0.0006 ns |  0.38 |         - |          NA |
