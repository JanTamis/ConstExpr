# Copilot repository instructions (short)

- Use BenchmarkDotNet for all performance/micro-benchmarks.
- Place benchmark projects in Benchmarks/ and include a Program.cs with BenchmarkSwitcher.
- Prefer using var where the type is obvious (use var when possible).
- All comments and documentation must be written in English.
- Run locally / in CI:

  dotnet run -c Release --project Benchmarks/Benchmarks.csproj -- --filter *

- Prefer [MemoryDiagnoser]; avoid network or I/O in microbenchmarks.
- These are suggestions for Copilot — combine with real examples and a CI workflow to enforce the convention.