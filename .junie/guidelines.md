# ConstExpr Development Guidelines

## Build & Configuration

- **Required SDK**: .NET 10.0+ (`global.json` pins to `10.0.0` with `latestMinor` roll-forward, `allowPrerelease: true`).
- **Solution file**: `ConstExpr.sln` at the repo root — use this to build all projects together.
- **Source generator target**: `netstandard2.0`; all other projects target `net10.0`.
- **Language version**: `preview` across all projects (C# 14 features are used freely).
- **Enabling the generator in a consuming project**: add `<UseConstExpr>true</UseConstExpr>` to the consuming project's `.csproj`. Without this MSBuild property the generator pipeline is gated off entirely.
- **NuGet packages** are restored automatically; no extra steps beyond `dotnet restore` (or a standard IDE build) are required.

## Running Tests

The test project uses **TUnit** on **Microsoft Testing Platform** (not VSTest/xUnit/NUnit).

```bash
# Run all tests
dotnet test --project ConstExpr.Tests --disable-logo --no-progress

# Run all tests in a specific class
dotnet test --project ConstExpr.Tests --disable-logo --no-ansi --no-progress \
  --treenode-filter '/*/*/IsNegativeTest/*'

# Run a single named test (use exact class + test name)
dotnet test --project ConstExpr.Tests --disable-logo --no-ansi --no-progress \
  --treenode-filter '/ConstExpr.Tests/ConstExpr.Tests.Validation/IsNegativeTest/RunTest*'

# List all tests (useful to discover FQNs)
dotnet test --project ConstExpr.Tests --list-tests --disable-logo

# Filter by name pattern
dotnet test --project ConstExpr.Tests --list-tests --disable-logo | grep 'IsNegativeTest'
```

`--treenode-filter` supports `*` wildcards, `|` (OR), `&` (AND), and `!=` negation — see AGENTS.md for full syntax.

## Writing New Tests

### Location
Place tests under `ConstExpr.Tests/Tests/<Category>/` where `<Category>` matches an existing folder (`Arithmetic`, `Array`, `Color`, `Linq`, `Math`, `NumberTheory`, `Optimization`, `Regex`, `Rewriter`, `String`, `Validation`).

### Pattern
Every test class:
1. Inherits `BaseTest<TDelegate>` where `TDelegate` is the signature of the method under test (e.g., `Func<int, bool>`).
2. Passes `FastMathFlags` (and optionally `LinqOptimisationMode`) to the base constructor.
3. Overrides `TestMethod` using `GetString(lambda)` — the lambda body becomes a local function that the rewriter processes.
4. Overrides `TestCases` returning `KeyValuePair<string?, object?[]>` entries via the `Create(...)` helper.
5. Is decorated with `[InheritsTests]` so TUnit picks up the `[Test]` defined in `BaseTest`.

### `Create(...)` helpers
| Overload | Meaning |
|---|---|
| `Create(null, Unknown)` | Expect body **unchanged** for an unknown-value input |
| `Create("return true;", value)` | Expect the given rewritten body when input is the literal value |
| `Create(null)` | All parameters treated as `Unknown`; expect body unchanged |
| `Create(lambdaDelegate, params)` | Expected body extracted from a lambda via `CallerArgumentExpression` |

> **Important**: `null` as the expected body means "the rewritten body must equal the *original formatted body*". If an optimizer transforms the method even for unknown inputs (e.g., `FastMathFlags.FastMath` rewrites `n % 2 == 0` → `Int32.IsEvenInteger(n)`), you must supply the transformed string instead of `null`.

### Minimal example

```csharp
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsEvenTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
    public override string TestMethod => GetString(n => n % 2 == 0);

    public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
    [
        Create("return Int32.IsEvenInteger(n);", Unknown), // FastMath rewrites even for unknown inputs
        Create("return true;", 4),
        Create("return false;", 7),
    ];
}
```

### `FastMathFlags` and `LinqOptimisationMode`
- Use `FastMathFlags.Strict` (= 0) when the method must obey strict IEEE 754 and no structural rewrites should occur for unknown inputs.
- Use `FastMathFlags.FastMath` to enable all fast-math optimisations (associativity, no-NaN, no-Inf, no-signed-zero, reciprocal, round-to-nearest, no-trapping, FMA).
- `LinqOptimisationMode.Unroll` (default) unrolls LINQ chains into imperative loops; `Optimize` folds constants without unrolling; `None` leaves LINQ untouched.

### extern alias
The test project references the source generator assembly under the alias `sourcegen`. Any generator-internal types (rewriters, models, visitors, etc.) must be prefixed with `sourcegen::` in test code:

```csharp
extern alias sourcegen;
using sourcegen::ConstExpr.SourceGenerator.Rewriters;
```

This alias is set via `<Aliases>sourcegen</Aliases>` in the `<ProjectReference>` inside `ConstExpr.Tests.csproj`.

## Adding a New Optimizer

### Function optimizer (most common)
1. Create a class in `Vectorize/ConstExpr.SourceGenerator/Optimizers/FunctionOptimizers/<Family>Optimizers/` (e.g., `MathOptimizers/`).
2. Inherit the matching typed base: `BaseMathFunctionOptimizer`, `BaseStringFunctionOptimizer`, `BaseLinqFunctionOptimizer`, `BaseRegexFunctionOptimizer`, or `BaseSimdFunctionOptimizer`.
3. Override `TryOptimize` (or the family-specific variant like `TryOptimizeMath`) — **no registration needed**; optimizers are discovered at runtime via reflection.
4. Access visited (already-rewritten) arguments via `context.VisitedParameters`.
5. Emit helper methods by calling `ParseMethodFromString(...)` and adding them to `context.AdditionalSyntax`.
6. Use `CreateInvocation(...)` to generate call-site `SyntaxNode` results.

### Binary optimizer
Implement `IBinaryStrategy` and add it to the relevant optimizer's `GetStrategies()` in the appropriate `*Strategies` folder under `Optimizers/BinaryOptimizers/`.

## Project Structure Notes
- `Vectorize/ConstExpr.SourceGenerator/Rewriters/` — the main rewrite pipeline (~15 partial-class files for `ConstExprPartialRewriter`).
- `Vectorize/ConstExpr.SourceGenerator/Operators/` — typed `IOperation` wrappers used during constant folding.
- `Vectorize/ConstExpr.SourceGenerator/Visitors/` — syntax/operation-tree traversal helpers.
- `Vectorize/ConstExpr.SourceGenerator/Builders/` — code-gen helpers (`EnumerableBuilder`, `InterfaceBuilder`, `MemoryExtensionsBuilder`).
- `Vectorize/ConstExpr.SourceGenerator/Models/` — shared value types (`FunctionOptimizerContext`, `VariableItem`, `VariableItemDictionary`).
- `SourceGen.Utilities/` — Roslyn extension methods shared across generator components.
- `Benchmarks/` — BenchmarkDotNet benchmarks; use `[MemoryDiagnoser]` and `BenchmarkSwitcher` in `Program.cs`.

## Code Style
- Use `var` where the type is obvious from the right-hand side.
- All comments and XML doc in English.
- `LangVersion` is `preview` — C# 14 / preview features are intentional, not accidental.
- No special formatting configuration beyond the defaults observed in existing files (tabs for indentation).
