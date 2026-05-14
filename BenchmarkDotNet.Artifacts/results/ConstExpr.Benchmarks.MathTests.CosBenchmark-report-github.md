```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  Job-IJPESX : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a

IterationCount=5  LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetCos_Double | Double     | 2.8786 ns | 0.0281 ns | 0.0073 ns |  1.00 |         - |          NA |
| FastCos_Double   | Double     | 0.9908 ns | 0.0056 ns | 0.0015 ns |  0.34 |         - |          NA |
| FastCosV2_Double | Double     | 0.7516 ns | 0.0038 ns | 0.0010 ns |  0.26 |         - |          NA |
|                  |            |           |           |           |       |           |             |
| DotNetCos_Float  | Float      | 2.5840 ns | 0.0134 ns | 0.0021 ns |  1.00 |         - |          NA |
| FastCos_Float    | Float      | 0.9427 ns | 0.0466 ns | 0.0121 ns |  0.36 |         - |          NA |
| FastCosV2_Float  | Float      | 0.6933 ns | 0.0038 ns | 0.0010 ns |  0.27 |         - |          NA |
| FastCosV3_Float  | Float      | 0.6777 ns | 0.0070 ns | 0.0018 ns |  0.26 |         - |          NA |
