# Copilot Instructions for ConstExpr

## Benchmarking Guidelines

- **Use BenchmarkDotNet** for all performance tests
- Place benchmark code in the `Benchmarks/` folder
- Create a `Program.cs` with `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` to support selecting benchmarks at runtime
- Add `[MemoryDiagnoser]` attribute to benchmark classes to track memory allocations
- Avoid network I/O and file I/O in microbenchmarks to ensure consistent measurements

## Code Style Conventions

- Use `var` for local variable declarations where the type is obvious from the right-hand side
- Write all comments and documentation in English
- Follow the existing code style patterns in the repository

---

# Project Architecture & Testing Guide (from AGENTS.md)

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

Additional internal components in `Vectorize/ConstExpr.SourceGenerator/`:
- `Operators/` — typed wrappers around Roslyn `IOperation` nodes (e.g., `BinaryOperation`, `InvocationOperation`) used during constant folding
- `Visitors/` — `ConstExprOperationVisitor`, `ConstExprPartialVisitor`, and `ExpressionVisitor` for traversing syntax/operation trees
- `Builders/` — code-generation helpers (`EnumerableBuilder`, `InterfaceBuilder`, `MemoryExtensionsBuilder`) used by unrollers and optimizers
- `Models/` — shared value types: `FunctionOptimizerContext`, `VariableItem`, `VariableItemDictionary`

The generator is gated by `<UseConstExpr>true</UseConstExpr>` in the consuming project's `.csproj`.

The source generator also ships **Roslyn analyzers** (`Analyzers/`), **code fixers** (`Fixers/`), and **refactorings** (`Refactorers/`) that provide IDE diagnostics and quick-fix suggestions for `[ConstExpr]`-annotated code.

## Adding a New Optimizer

To add a function optimizer (e.g., for a new Math method):

1. Create a class in the matching subfolder under `Optimizers/FunctionOptimizers/` (e.g., `MathOptimizers/`)
2. Inherit the appropriate base: `BaseMathFunctionOptimizer`, `BaseStringFunctionOptimizer`, `BaseLinqFunctionOptimizer`, `BaseRegexFunctionOptimizer`, or `BaseSimdFunctionOptimizer`
3. Override `TryOptimize(FunctionOptimizerContext, out SyntaxNode?)` — no registration needed; **optimizers are discovered via reflection** in `ConstExprPartialRewriter`
4. For binary optimizers, implement `IBinaryStrategy` and add it to the relevant optimizer's `GetStrategies()`

## Test Commands

```bash
# Run all tests (TUnit on Microsoft.Testing.Platform)
TUNIT_DISABLE_HTML_REPORTER=true dotnet test --project ConstExpr.Tests --disable-logo --no-progress

# Run tests from class
TUNIT_DISABLE_HTML_REPORTER=true dotnet test --project ConstExpr.Tests --disable-logo --no-ansi --no-progress --treenode-filter '/*/*/<Class name>/*'

# Run specific test
dotnet test --project ConstExpr.Tests --disable-logo --no-ansi --no-progress --treenode-filter '/<Assembly>/<Namespace>/<Class name>/<Test name>'

# Get List of all test with specific name pattern (e.g., all tests in AbsoluteDifferenceTest class)
dotnet test --project ConstExpr.Tests --list-tests --disable-logo | grep 'AbsoluteDifferenceTest'
```

**Filter syntax:**
- Wildcard matching: Use `*` for pattern matching (e.g., `LoginTests*` matches LoginTests, LoginTestsSuite, etc.)
- Equality: Use `=` for exact match (e.g., `[Category=Unit]`)
- Negation: Use `!=` for excluding values (e.g., `[Category!=Performance]`)
- AND: Use `&` to combine conditions within a single path segment (e.g., `/*/*/(ClassA*)&(*Smoke)/*`)
- OR: Use `|` similarly (e.g., `/*/*/(Class1)|(Class2)/*`)
- Match-all: `**` matches any path depth (e.g., `/**` or `/MyAssembly/**`)

## IDE Tool Usage

**Always prefer IDE tools over the terminal (except for unit tests)** for any task that the IDE tools can perform.

### Build / Debug
- ✅ Build: Use `mcp_rider_build_solution` instead of `dotnet build`
- ✅ Debug: Use debugger tools (`mcp_rider-debugge_start_debug_session`, `mcp_rider-debugge_step_over`, etc.)

### Code Navigation & Refactoring
- ✅ Symbols: Use `mcp_rider_search_symbol`, `mcp_rider_get_symbol_info`
- ✅ Find files: Use `mcp_rider_search_file`, `mcp_rider_find_files_by_name_keyword`
- ✅ Search: Use `mcp_rider_search_text`, `mcp_rider_search_regex`
- ✅ Rename: Use `mcp_rider_rename_refactoring`

### Testing
- ⚠️ Terminal only: Use `dotnet test` commands (execute_terminal_command)

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

Tests live under `ConstExpr.Tests/Tests/{Arithmetic,Array,Color,Linq,Math,NumberTheory,Optimization,Regex,Rewriter,String,Validation}/`.

**Important**: The test project uses `extern alias sourcegen;` to reference the generator assembly. Prefix generator types with `sourcegen::` in test code.

**Important**: when editing code, always run the full test suite to ensure no regressions. Use the filtering commands to run specific tests during development, but validate with the full suite

