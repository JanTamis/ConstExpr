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

## Build & Test Commands

# Run all tests (TUnit on Microsoft.Testing.Platform)
dotnet test --project ConstExpr.Tests --disable-logo

# Get List of all Tests with their Fully Qualified Names (FQNs)
dotnet test --project ConstExpr.Tests --list-tests


# Run specific test
dotnet test --project ConstExpr.Tests --treenode-filter /<Assembly>/<Namespace>/<Class name>/<Test name>

- Wildcard matching: Use * for pattern matching (e.g., LoginTests* matches LoginTests, LoginTestsSuite, etc.)
- Equality: Use = for exact match (e.g., [Category=Unit])
- Negation: Use != for excluding values (e.g., [Category!=Performance])
- Use & to combine conditions within a single path segment or property group, with each side wrapped in parentheses. Examples:
  - AND across path patterns: /*/*/(ClassA*)&(*Smoke)/*
  - AND across properties: /**[(Category=Unit)&(Priority=High)]
- OR operator: Use | the same way — within a single segment or property group, with parentheses. Examples:
  - OR across classes: /*/*/(Class1)|(Class2)/*
  - OR across properties: /**[(Category=Smoke)|(Priority=High)]
- Match-all: ** matches any path depth (e.g., /** or /MyAssembly/**). It must appear at the end of the path — /**/Path is not allowed.

## IDE Tool Usage

**Always prefer IDE tools over the terminal** for any task that the IDE tools can perform. Never use terminal commands when an equivalent IDE tool is available.

| Task | Use IDE tool | Do NOT use terminal |
|---|---|---|
| Build / compile | `build_project` | `dotnet build` |
| Discover run configurations | `get_run_configurations` | — |
| Start or stop a debug session | `rider-debugger_list_run_configurations` / `rider-debugger_start_debug_session` / `rider-debugger_stop_debug_session` | `lldb` / `gdb` / ad-hoc CLI debugging |
| Resume, pause, or run to a line while debugging | `rider-debugger_resume_execution` / `rider-debugger_pause_execution` / `rider-debugger_run_to_line` / `rider-debugger_wait_for_pause` | manual debugger commands in a terminal |
| Step through code while debugging | `rider-debugger_step_over` / `rider-debugger_step_into` / `rider-debugger_step_out` | manual debugger commands in a terminal |
| Manage breakpoints or logpoints | `rider-debugger_set_breakpoint` / `rider-debugger_list_breakpoints` / `rider-debugger_remove_breakpoint` | `break` / `b` / other CLI breakpoint commands |
| Inspect debug state, threads, or source context | `rider-debugger_get_debug_session_status` / `rider-debugger_get_source_context` / `rider-debugger_get_stack_trace` / `rider-debugger_list_threads` | `bt` / `thread list` / other CLI inspection commands |
| Inspect or modify variables while debugging | `rider-debugger_get_variables` / `rider-debugger_select_stack_frame` / `rider-debugger_evaluate_expression` / `rider-debugger_set_variable` | `print` / `frame variable` / `expr` |
| Read file contents | `get_file_text_by_path` / `read_file` | `cat` / `less` |
| Find files | `find_files_by_name_keyword` / `find_files_by_glob` / `search_file` | `find` / `ls` |
| Search in files | `search_in_files_by_text` / `search_in_files_by_regex` / `search_text` / `search_regex` | `grep` / `rg` |
| Rename a symbol | `rename_refactoring` | manual search-and-replace |
| Browse project structure | `list_directory_tree` | `ls` / `tree` |
| Check errors / warnings | `get_file_problems` | — |
| Get symbol information | `get_symbol_info` | — |

Only fall back to the terminal for tasks that cannot be performed with any IDE tool (e.g., package management commands like `dotnet add package`).

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
    public override IEnumerable<KeyValuePair<string?, obj~~~~ect?[]>> TestCases =>
    [
        Create(null, Unknown),        // Unknown input → body unchanged (null = expect original)
        Create("return true;", -10),  // Constant -10 → rewritten to "return true;"
        Create("return false;", 0)
    ];
}
```

Tests live under `ConstExpr.Tests/Tests/{Arithmetic,Array,Color,Linq,Math,NumberTheory,Optimization,Regex,Rewriter,String,Validation}/`.

**Important**: The test project uses `extern alias sourcegen;` to reference the generator assembly. Prefix generator types with `sourcegen::` in test code.

## Code Style

- Use `var` where type is obvious from the right-hand side
- All comments and documentation in English
- `LangVersion` is `preview` across all projects
- Benchmarks go in `Benchmarks/` with `[MemoryDiagnoser]` and `BenchmarkSwitcher` in `Program.cs`
