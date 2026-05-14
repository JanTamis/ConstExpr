```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method           | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetExp_Double | Double     | 2.4704 ns | 0.0076 ns | 0.0067 ns |  1.00 |         - |          NA |
| FastExp_Double   | Double     | 1.0203 ns | 0.0040 ns | 0.0033 ns |  0.41 |         - |          NA |
| FastExpV2_Double | Double     | 0.8942 ns | 0.0009 ns | 0.0008 ns |  0.36 |         - |          NA |
| FastExpV3_Double | Double     | 0.9426 ns | 0.0007 ns | 0.0006 ns |  0.38 |         - |          NA |
|                  |            |           |           |           |       |           |             |
| DotNetExp_Float  | Float      | 1.5003 ns | 0.0015 ns | 0.0014 ns |  1.00 |         - |          NA |
| FastExp_Float    | Float      | 1.0029 ns | 0.0012 ns | 0.0010 ns |  0.67 |         - |          NA |
| FastExpV2_Float  | Float      | 0.8262 ns | 0.0041 ns | 0.0034 ns |  0.55 |         - |          NA |
| FastExpV3_Float  | Float      | 0.9558 ns | 0.0015 ns | 0.0014 ns |  0.64 |         - |          NA |
