```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method               | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetAcosPi_Double  | Double     | 3.0647 ns | 0.0096 ns | 0.0090 ns |  1.00 |         - |          NA |
| OldFastAcosPi_Double | Double     | 1.1952 ns | 0.0035 ns | 0.0033 ns |  0.39 |         - |          NA |
| NewFastAcosPi_Double | Double     | 0.9577 ns | 0.0033 ns | 0.0031 ns |  0.31 |         - |          NA |
|                      |            |           |           |           |       |           |             |
| DotNetAcosPi_Float   | Float      | 2.9180 ns | 0.0123 ns | 0.0109 ns |  1.00 |         - |          NA |
| OldFastAcosPi_Float  | Float      | 1.1429 ns | 0.0026 ns | 0.0024 ns |  0.39 |         - |          NA |
| NewFastAcosPi_Float  | Float      | 0.9969 ns | 0.0106 ns | 0.0099 ns |  0.34 |         - |          NA |
