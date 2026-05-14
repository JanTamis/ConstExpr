```

BenchmarkDotNet v0.14.0, macOS 26.2 (25C5031i) [Darwin 25.2.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]     : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 9.0.1 (9.0.124.61010), Arm64 RyuJIT AdvSIMD


```
| Method                  | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| ManualShifting          | 2.675 ns | 0.0141 ns | 0.0110 ns |  1.00 |    0.01 | 0.0038 |      32 B |        1.00 |
| BitConverterGetBytes    | 2.918 ns | 0.0164 ns | 0.0128 ns |  1.09 |    0.01 | 0.0038 |      32 B |        1.00 |
| MemoryMarshalWrite      | 2.475 ns | 0.0066 ns | 0.0051 ns |  0.93 |    0.00 | 0.0038 |      32 B |        1.00 |
| StackAllocMemoryMarshal | 2.716 ns | 0.0685 ns | 0.0608 ns |  1.02 |    0.02 | 0.0038 |      32 B |        1.00 |
