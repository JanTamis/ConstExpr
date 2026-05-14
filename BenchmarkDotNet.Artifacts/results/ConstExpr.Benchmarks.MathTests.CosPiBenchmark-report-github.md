```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                  | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------------ |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetCosPi_Double      | Double     | 2.5086 ns | 0.0028 ns | 0.0023 ns |  1.00 |         - |          NA |
| CurrentFastCosPi_Double | Double     | 1.4928 ns | 0.0187 ns | 0.0166 ns |  0.60 |         - |          NA |
| FastCosPiV2_Double      | Double     | 1.1310 ns | 0.0012 ns | 0.0011 ns |  0.45 |         - |          NA |
|                         |            |           |           |           |       |           |             |
| DotNetCosPi_Float       | Float      | 2.2524 ns | 0.0249 ns | 0.0233 ns |  1.00 |         - |          NA |
| CurrentFastCosPi_Float  | Float      | 1.4778 ns | 0.0071 ns | 0.0066 ns |  0.66 |         - |          NA |
| FastCosPiV2_Float       | Float      | 1.0000 ns | 0.0018 ns | 0.0014 ns |  0.44 |         - |          NA |
| FastCosPiV3_Float       | Float      | 0.9359 ns | 0.0007 ns | 0.0007 ns |  0.42 |         - |          NA |
