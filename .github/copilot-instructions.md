# Copilot Instructions for ConstExpr

## Benchmarking Guidelines

- **Use BenchmarkDotNet** for all performance tests
- Place benchmark code in the `Benchmarks/` folder
- Create a `Program.cs` with `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` to support selecting benchmarks at runtime
- Add `[MemoryDiagnoser]` attribute to benchmark classes to track memory allocations
- Avoid network I/O and file I/O in microbenchmarks to ensure consistent measurements

### Running Benchmarks

**Local:**
```bash
dotnet run -c Release --project Benchmarks/YourBenchmark.csproj
```

**CI:**
```bash
dotnet run -c Release --project Benchmarks/YourBenchmark.csproj --filter '*'
```

## Code Style Conventions

- Use `var` for local variable declarations where the type is obvious from the right-hand side
- Write all comments and documentation in English
- Follow the existing code style patterns in the repository
