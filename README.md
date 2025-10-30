# ConstExpr

A C# source generator that brings compile-time constant expression evaluation to your codebase.

Ever wish your constant calculations happened at build time instead of runtime? ConstExpr makes it happen automatically. It detects constant expressions, evaluates them during compilation, and generates optimized code with pre-computed values.

```csharp
// You write this:
var result = MathOps.IsPrime(17);

// ConstExpr generates this at compile time:
var result = true;
```

## What is ConstExpr?

ConstExpr is a Roslyn-based source generator that analyzes your code looking for opportunities to optimize. When it finds method calls or expressions that can be evaluated at compile time, it automatically generates optimized code with the values already computed.

The benefits? Faster applications, smaller binaries, and you still get to write clean, expressive code.

## Features

- **Compile-Time Evaluation** - Automatically evaluates constant expressions during compilation
- **Performance Boost** - Eliminates runtime overhead by pre-computing values
- **Flexible Floating-Point Control** - Choose between strict IEEE 754 compliance or fast-math optimizations
- **Wide Operation Support** - Works with:
  - Mathematical operations (statistics, prime detection, numeric algorithms)
  - String manipulation (formatting, encoding, text processing)
  - Collection operations (LINQ queries, transformations, filtering)
  - Date/Time calculations
  - Geometry and physics computations
  - Financial calculations
  - Color transformations
- **Smart Caching** - Incremental generation means fast rebuilds
- **Drop-in Integration** - Works seamlessly with your existing code

## Getting Started

### Installation

Add ConstExpr to your project via NuGet:

```bash
dotnet add package ConstExpr.SourceGenerator
```

Or add it directly to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="ConstExpr.SourceGenerator" Version="0.1.19-preview" />
</ItemGroup>
```

### Basic Setup

First, enable ConstExpr in your `.csproj`:

```xml
<PropertyGroup>
  <UseConstExpr>true</UseConstExpr>
</PropertyGroup>
```

Then, mark the methods you want to optimize with the `[ConstExpr]` attribute:

```csharp
using ConstExpr.Core.Attributes;

[ConstExpr]
public static class MathOperations
{
    public static double Add(double a, double b)
    {
        return a + b;
    }
    
    public static bool IsPrime(int number)
    {
        if (number < 2) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;
        
        var sqrt = (int)Math.Sqrt(number);
        for (var i = 3; i <= sqrt; i += 2)
        {
            if (number % i == 0) return false;
        }
        return true;
    }
}
```

### How It Optimizes

ConstExpr works in multiple ways depending on your code:

**Full optimization** happens when all arguments are constants:

```csharp
var sum = MathOperations.Add(10.5, 20.3);
var isPrime = MathOperations.IsPrime(17);

// ✨ Becomes this at compile time:
var sum = 30.8;
var isPrime = true;
```

**Partial evaluation** works even when mixing constants with variables:

```csharp
var x = 5.0;
var result = MathOperations.Add(10.5, x);
// ConstExpr optimizes what it can, even with variables
```

## Going Deeper

### Floating-Point Modes

You can control how ConstExpr handles floating-point math:

```csharp
// Strict mode - exact IEEE 754 behavior (default)
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.Strict)]
public static class StrictMath
{
    // Perfect precision, always
}

// Fast mode - allows some flexibility for better performance
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class FastMath
{
    // Faster, with minor precision tradeoffs
}
```

### More Examples

Here's ConstExpr working with string operations:

```csharp
[ConstExpr]
public static class StringOps
{
    public static string FormatFullName(string firstName, string lastName)
    {
        return $"{firstName} {lastName}";
    }
    
    public static int StringLength(string value, Encoding encoding)
    {
        return encoding.GetByteCount(value);
    }
}

// These get optimized at compile time too
var name = StringOps.FormatFullName("John", "Doe");
var length = StringOps.StringLength("Hello", Encoding.UTF8);
```

## Under the Hood

Here's what happens when ConstExpr processes your code:

1. **Detection** - Scans for methods marked with `[ConstExpr]`
2. **Analysis** - Figures out what can be computed at compile time
3. **Evaluation** - Actually runs the optimizable code during compilation
4. **Generation** - Creates new code with the computed values
5. **Caching** - Remembers results for fast incremental builds

The cool part? ConstExpr handles both full constant folding (all arguments constant) and partial evaluation (mix of constants and variables).

## Good to Know

A few things to keep in mind:

- ✅ Full optimization when all arguments are constants
- ✅ Partial evaluation when mixing constants with variables
- ⚠️ Can't optimize code with runtime dependencies
- ⚠️ Complex dynamic behavior won't be optimized

## Contributing

Found a bug or have an idea? Contributions are welcome! Feel free to open an issue or submit a pull request.

## License

MIT License

## Author

Created by Jan Tamis - [GitHub](https://github.com/JanTamis)

## Links

- 📦 [NuGet Package](https://www.nuget.org/packages/ConstExpr.SourceGenerator)
- 💻 [GitHub Repository](https://github.com/JanTamis/ConstExpr)
