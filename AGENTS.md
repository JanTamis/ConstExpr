# AGENTS.md

## Project Overview

ConstExpr is a **Roslyn incremental source generator** that evaluates constant expressions at compile time. Methods marked with `[ConstExpr]` are analyzed; when called with constant arguments, their results are pre-computed and inlined during compilation.

## Architecture

**5 projects** in `ConstExpr.sln` (the source generator targets `netstandard2.0`; tests target `net10.0`):

| Project | Purpose |
|---|---|
| `Vectorize/ConstExpr.Core/` | `[ConstExpr]`/`[ConstEval]` attributes, `FastMathFlags` flags enum, and `LinqOptimisationMode` enum — the public API consumed by users |
| `Vectorize/ConstExpr.SourceGenerator/` | The Roslyn source generator (packaged as NuGet analyzer) |
| `SourceGen.Utilities/` | Shared Roslyn helper extensions used by the generator |
| `ConstExpr.Tests/` | Unit tests (TUnit framework, **not** xUnit/NUnit) |
| `Vectorize/ConstExpr.Sample/` | Sample project exercising the generator |

### Generator Pipeline (`ConstExprSourceGenerator.cs`)

1. **Detect** `InvocationExpressionSyntax` nodes → filter for `[ConstExpr]`-attributed methods
2. **Rewrite** via `ConstExprPartialRewriter` (partial class split across ~15 files in `Rewriters/`)
3. **Optimize** using three optimizer families discovered by reflection at runtime:
   - `Optimizers/FunctionOptimizers/{Math,Linq,String,Regex,Simd}Optimizers/` — inherit `BaseFunctionOptimizer`, override `TryOptimize`
   - `Optimizers/BinaryOptimizers/` — inherit `BaseBinaryOptimizer`, use strategy pattern (`IBinaryStrategy`)
   - `Optimizers/ConditionalOptimizers/`
   - `Optimizers/LinqUnrollers/` — inherit `BaseLinqUnroller`, unroll LINQ chains into imperative code (controlled by `LinqOptimisationMode.Unroll`)
4. **Prune** dead code via `DeadCodePruner`, format via `FormattingHelper`
5. **Intercept** via generated `[InterceptsLocation]` attributes — the generator emits interceptor methods that replace call sites

The generator is gated by `<UseConstExpr>true</UseConstExpr>` in the consuming project's `.csproj`.

The source generator also ships **Roslyn analyzers** (`Analyzers/`), **code fixers** (`Fixers/`), and **refactorings** (`Refactorers/`) that provide IDE diagnostics and quick-fix suggestions for `[ConstExpr]`-annotated code.

## Build & Test Commands

```bash
# Build entire solution
dotnet build ConstExpr.sln

# Run all tests (TUnit on Microsoft.Testing.Platform)
dotnet test --project ConstExpr.Tests

# Run benchmarks (BenchmarkDotNet, always Release)
dotnet run -c Release --project Benchmarks/Benchmarks.csproj
```

## Adding a New Optimizer

To add a function optimizer (e.g., for a new Math method):

1. Create a class in the matching subfolder under `Optimizers/FunctionOptimizers/` (e.g., `MathOptimizers/`)
2. Inherit the appropriate base: `BaseMathFunctionOptimizer`, `BaseStringFunctionOptimizer`, `BaseLinqFunctionOptimizer`, `BaseRegexFunctionOptimizer`, or `BaseSimdFunctionOptimizer`
3. Override `TryOptimize(FunctionOptimizerContext, out SyntaxNode?)` — no registration needed; **optimizers are discovered via reflection** in `ConstExprPartialRewriter`
4. For binary optimizers, implement `IBinaryStrategy` and add it to the relevant optimizer's `GetStrategies()`

## Writing Tests

Tests use **TUnit** and a specific `BaseTest<TDelegate>` pattern. Each test:

- Inherits `BaseTest<TDelegate>` with the method signature as `TDelegate` (e.g., `Func<int, bool>`)
- Overrides `TestMethod` using `GetString(lambda)` — the lambda IS the code under test
- Overrides `TestCases` returning expected rewritten bodies for given inputs
- Uses `Unknown` sentinel for parameters without known constant values (simulates partial evaluation)
- Uses `[InheritsTests]` attribute

Example (`ConstExpr.Tests/Tests/Validation/IsNegativeTest.cs`):
```csharp
[InheritsTests]
public class IsNegativeTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
    public override string TestMethod => GetString(n => n < 0);
    public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
    [
        Create(null, Unknown),        // Unknown input → body unchanged (null = expect original)
        Create("return true;", -10),  // Constant -10 → rewritten to "return true;"
        Create("return false;", 0)
    ];
}
```

Tests live under `ConstExpr.Tests/Tests/{Arithmetic,Array,Linq,Math,NumberTheory,Optimization,Regex,Rewriter,String,Validation}/`.

**Important**: The test project uses `extern alias sourcegen;` to reference the generator assembly. Prefix generator types with `sourcegen::` in test code.

## Code Style

- Use `var` where type is obvious from the right-hand side
- All comments and documentation in English
- `LangVersion` is `preview` across all projects
- Benchmarks go in `Benchmarks/` with `[MemoryDiagnoser]` and `BenchmarkSwitcher` in `Program.cs`

