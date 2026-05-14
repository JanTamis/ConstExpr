```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method         | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|----------:|------------:|
| DotNetLerp     | 0.6778 ns | 0.0029 ns | 0.0024 ns |  1.00 |         - |          NA |
| FmaLerp        | 0.6752 ns | 0.0020 ns | 0.0016 ns |  1.00 |         - |          NA |
| NaiveLerp      | 0.6778 ns | 0.0008 ns | 0.0007 ns |  1.00 |         - |          NA |
| ComplementLerp | 0.6790 ns | 0.0007 ns | 0.0006 ns |  1.00 |         - |          NA |
