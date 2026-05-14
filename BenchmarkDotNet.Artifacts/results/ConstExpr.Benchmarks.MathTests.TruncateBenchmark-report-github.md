```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNet_Double      | Double     | 0.5421 ns | 0.0041 ns | 0.0032 ns |  1.00 |    0.01 |         - |          NA |
| BitManip_Double    | Double     | 0.6291 ns | 0.0003 ns | 0.0003 ns |  1.16 |    0.01 |         - |          NA |
| LongCast_Double    | Double     | 0.6087 ns | 0.0016 ns | 0.0015 ns |  1.12 |    0.01 |         - |          NA |
| GenericMath_Double | Double     | 0.5554 ns | 0.0040 ns | 0.0036 ns |  1.02 |    0.01 |         - |          NA |
|                    |            |           |           |           |       |         |           |             |
| DotNet_Float       | Float      | 0.5588 ns | 0.0032 ns | 0.0030 ns |  1.00 |    0.01 |         - |          NA |
| BitManip_Float     | Float      | 0.6927 ns | 0.0138 ns | 0.0129 ns |  1.24 |    0.02 |         - |          NA |
| IntCast_Float      | Float      | 0.6151 ns | 0.0009 ns | 0.0008 ns |  1.10 |    0.01 |         - |          NA |
| GenericMath_Float  | Float      | 0.5620 ns | 0.0023 ns | 0.0020 ns |  1.01 |    0.01 |         - |          NA |
