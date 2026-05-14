```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C5031i) [Darwin 25.2.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]     : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD


```
| Method                  | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| ManualShifting          | 2.628 ns | 0.0168 ns | 0.0149 ns |  1.00 |    0.01 | 0.0038 |      32 B |        1.00 |
| BitConverterGetBytes    | 2.976 ns | 0.0504 ns | 0.0421 ns |  1.13 |    0.02 | 0.0038 |      32 B |        1.00 |
| MemoryMarshalWrite      | 2.570 ns | 0.0093 ns | 0.0082 ns |  0.98 |    0.01 | 0.0038 |      32 B |        1.00 |
| StackAllocMemoryMarshal | 2.762 ns | 0.0338 ns | 0.0316 ns |  1.05 |    0.01 | 0.0038 |      32 B |        1.00 |
