```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C5031i) [Darwin 25.2.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]     : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD


```
| Method            | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0     | Gen1    | Gen2   | Allocated | Alloc Ratio |
|------------------ |-------------:|-----------:|-----------:|-------:|--------:|---------:|--------:|-------:|----------:|------------:|
| Structural_Small  |     2.494 μs |  0.0100 μs |  0.0089 μs |   0.22 |    0.00 |   0.5836 |       - |      - |   4.78 KB |        0.46 |
| Structural_Medium |     4.432 μs |  0.0286 μs |  0.0268 μs |   0.39 |    0.00 |   0.7706 |       - |      - |   6.33 KB |        0.61 |
| Structural_Large  |   509.817 μs |  7.3877 μs |  6.5490 μs |  44.62 |    0.56 |  46.8750 | 14.6484 | 0.9766 | 378.47 KB |       36.37 |
| Old_Small         |    11.426 μs |  0.0315 μs |  0.0246 μs |   1.00 |    0.00 |   1.4191 |  0.3967 | 0.1526 |  10.41 KB |        1.00 |
| Old_Medium        |    27.825 μs |  0.0894 μs |  0.0747 μs |   2.44 |    0.01 |   2.6245 |  0.7019 | 0.3052 |  18.95 KB |        1.82 |
| Old_Large         | 4,973.559 μs | 64.1987 μs | 53.6088 μs | 435.27 |    4.61 | 210.9375 | 46.8750 | 7.8125 | 1691.6 KB |      162.56 |
