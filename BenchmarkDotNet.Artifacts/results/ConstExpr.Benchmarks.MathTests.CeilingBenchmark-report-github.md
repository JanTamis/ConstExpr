```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method               | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetCeiling_Double | Double     | 0.5614 ns | 0.0021 ns | 0.0019 ns |  1.00 |         - |          NA |
| NegFloor_Double      | Double     | 0.5806 ns | 0.0064 ns | 0.0057 ns |  1.03 |         - |          NA |
| LongCast_Double      | Double     | 0.6666 ns | 0.0005 ns | 0.0004 ns |  1.19 |         - |          NA |
| GenericMath_Double   | Double     | 0.5703 ns | 0.0016 ns | 0.0014 ns |  1.02 |         - |          NA |
|                      |            |           |           |           |       |           |             |
| DotNetCeiling_Float  | Float      | 0.5716 ns | 0.0030 ns | 0.0023 ns |  1.00 |         - |          NA |
| NegFloor_Float       | Float      | 0.5874 ns | 0.0046 ns | 0.0041 ns |  1.03 |         - |          NA |
| IntCast_Float        | Float      | 0.6805 ns | 0.0044 ns | 0.0041 ns |  1.19 |         - |          NA |
| GenericMath_Float    | Float      | 0.5754 ns | 0.0012 ns | 0.0011 ns |  1.01 |         - |          NA |
