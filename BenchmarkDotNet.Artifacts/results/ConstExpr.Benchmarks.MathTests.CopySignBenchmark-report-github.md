```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method                      | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNetCopySign_Double       | Double     | 0.6365 ns | 0.0020 ns | 0.0018 ns |  1.00 |    0.00 |         - |          NA |
| BitConverterCopySign_Double | Double     | 0.5755 ns | 0.0022 ns | 0.0019 ns |  0.90 |    0.00 |         - |          NA |
| UnsafeCopySign_Double       | Double     | 0.5758 ns | 0.0038 ns | 0.0033 ns |  0.90 |    0.01 |         - |          NA |
| TernaryCopySign_Double      | Double     | 1.0391 ns | 0.0082 ns | 0.0076 ns |  1.63 |    0.01 |         - |          NA |
|                             |            |           |           |           |       |         |           |             |
| DotNetCopySign_Float        | Float      | 0.6427 ns | 0.0029 ns | 0.0024 ns |  1.00 |    0.01 |         - |          NA |
| BitConverterCopySign_Float  | Float      | 0.5769 ns | 0.0038 ns | 0.0034 ns |  0.90 |    0.01 |         - |          NA |
| UnsafeCopySign_Float        | Float      | 0.5832 ns | 0.0032 ns | 0.0026 ns |  0.91 |    0.01 |         - |          NA |
| TernaryCopySign_Float       | Float      | 1.0531 ns | 0.0110 ns | 0.0103 ns |  1.64 |    0.02 |         - |          NA |
