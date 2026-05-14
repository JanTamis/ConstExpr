```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                   | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNet_Double            | Double     | 0.5350 ns | 0.0038 ns | 0.0036 ns |  1.00 |    0.01 |         - |          NA |
| CurrentFast_Double       | Double     | 0.9658 ns | 0.0049 ns | 0.0044 ns |  1.81 |    0.01 |         - |          NA |
| UnsafeRangeCheck_Double  | Double     | 0.9978 ns | 0.0039 ns | 0.0037 ns |  1.87 |    0.01 |         - |          NA |
| BitCast_Double           | Double     | 0.9717 ns | 0.0077 ns | 0.0069 ns |  1.82 |    0.02 |         - |          NA |
| BitCastRangeCheck_Double | Double     | 1.0000 ns | 0.0059 ns | 0.0052 ns |  1.87 |    0.02 |         - |          NA |
| Branchless_Double        | Double     | 1.2275 ns | 0.0027 ns | 0.0024 ns |  2.29 |    0.02 |         - |          NA |
|                          |            |           |           |           |       |         |           |             |
| DotNet_Float             | Float      | 0.7551 ns | 0.0151 ns | 0.0362 ns |  1.00 |    0.07 |         - |          NA |
| CurrentFast_Float        | Float      | 1.5596 ns | 0.0109 ns | 0.0102 ns |  2.07 |    0.10 |         - |          NA |
| UnsafeRangeCheck_Float   | Float      | 1.4975 ns | 0.0119 ns | 0.0112 ns |  1.99 |    0.09 |         - |          NA |
| BitCast_Float            | Float      | 1.5601 ns | 0.0131 ns | 0.0122 ns |  2.07 |    0.10 |         - |          NA |
| BitCastRangeCheck_Float  | Float      | 1.5123 ns | 0.0202 ns | 0.0189 ns |  2.01 |    0.10 |         - |          NA |
| Branchless_Float         | Float      | 1.2187 ns | 0.0010 ns | 0.0008 ns |  1.62 |    0.08 |         - |          NA |
