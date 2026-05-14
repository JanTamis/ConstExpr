```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method              | Categories | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| DotNet_Double       | Double     | 2.050 ns | 0.0317 ns | 0.0296 ns |  1.00 |    0.02 |         - |          NA |
| Current_Double      | Double     | 2.055 ns | 0.0242 ns | 0.0227 ns |  1.00 |    0.02 |         - |          NA |
| NewtonHalley_Double | Double     | 2.199 ns | 0.0297 ns | 0.0278 ns |  1.07 |    0.02 |         - |          NA |
| ThreeNewton_Double  | Double     | 3.111 ns | 0.0524 ns | 0.0490 ns |  1.52 |    0.03 |         - |          NA |
|                     |            |          |           |           |       |         |           |             |
| DotNet_Float        | Float      | 2.280 ns | 0.0331 ns | 0.0309 ns |  1.00 |    0.02 |         - |          NA |
| Current_Float       | Float      | 2.116 ns | 0.0240 ns | 0.0224 ns |  0.93 |    0.02 |         - |          NA |
| NewtonHalley_Float  | Float      | 2.525 ns | 0.0255 ns | 0.0226 ns |  1.11 |    0.02 |         - |          NA |
| HalleyOnly_Float    | Float      | 1.064 ns | 0.0133 ns | 0.0124 ns |  0.47 |    0.01 |         - |          NA |
