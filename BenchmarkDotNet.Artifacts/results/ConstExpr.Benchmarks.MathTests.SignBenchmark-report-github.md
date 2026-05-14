```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.3.1 (a) (25D771280a) [Darwin 25.3.0]
Apple M4 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), Arm64 RyuJIT armv8.0-a


```
| Method             | Categories | Mean      | Error     | StdDev    | Allocated |
|------------------- |----------- |----------:|----------:|----------:|----------:|
| GenericSign_Double | Double     | 0.7582 ns | 0.0027 ns | 0.0024 ns |         - |
|                    |            |           |           |           |           |
| GenericSign_Float  | Float      | 0.7595 ns | 0.0014 ns | 0.0013 ns |         - |
|                    |            |           |           |           |           |
| GenericSign_Int    | Int        | 0.4792 ns | 0.0096 ns | 0.0202 ns |         - |
