```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method            | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------ |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetExp2_Double | Double     | 3.6156 ns | 0.0039 ns | 0.0037 ns |  1.00 |         - |          NA |
| FastExp2_Double   | Double     | 1.3193 ns | 0.0023 ns | 0.0021 ns |  0.36 |         - |          NA |
| FastExp2V2_Double | Double     | 0.9545 ns | 0.0009 ns | 0.0008 ns |  0.26 |         - |          NA |
| FastExp2V3_Double | Double     | 1.0202 ns | 0.0012 ns | 0.0012 ns |  0.28 |         - |          NA |
|                   |            |           |           |           |       |           |             |
| DotNetExp2_Float  | Float      | 2.4989 ns | 0.0018 ns | 0.0017 ns |  1.00 |         - |          NA |
| FastExp2_Float    | Float      | 1.3455 ns | 0.0021 ns | 0.0020 ns |  0.54 |         - |          NA |
| FastExp2V2_Float  | Float      | 0.9537 ns | 0.0019 ns | 0.0018 ns |  0.38 |         - |          NA |
| FastExp2V3_Float  | Float      | 1.0201 ns | 0.0010 ns | 0.0008 ns |  0.41 |         - |          NA |
