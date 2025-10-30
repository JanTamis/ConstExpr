# ConstExpr

A C# source generator that performs compile-time constant expression evaluation and optimization.

ConstExpr automatically detects and evaluates constant expressions at compile time, transforming runtime computations into compile-time constants. This eliminates unnecessary runtime overhead and improves application performance.

```csharp
// You write this:
var result = MathOps.IsPrime(17);

// ConstExpr generates this at compile time:
var result = true;
```

## Overview

ConstExpr is a Roslyn-based source generator that analyzes your code to identify opportunities for constant folding. When it finds method calls or expressions that can be evaluated at compile time, it automatically generates optimized code with pre-computed constant values.

This results in faster applications and smaller binaries while maintaining clean, readable code.

## Features

- **Compile-Time Evaluation**: Automatically evaluates constant expressions during compilation
- **Performance Optimization**: Eliminates runtime overhead by pre-computing constant values
- **Floating-Point Precision Control**: Choose between strict IEEE 754 compliance or fast-math optimizations
- **Comprehensive Operation Support**:
  - Mathematical operations (statistics, prime detection, numeric algorithms)
  - String manipulation (formatting, encoding, text processing)
  - Collection operations (LINQ queries, transformations, filtering)
  - Date/Time calculations
  - Geometry and physics computations
  - Financial calculations
  - Color transformations
- **Incremental Generation**: Efficient caching prevents unnecessary reprocessing
- **Seamless Integration**: Works with your existing codebase

## Installation

Install via NuGet:

```bash
dotnet add package ConstExpr.SourceGenerator
```

Or add to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="ConstExpr.SourceGenerator" Version="0.1.19-preview" />
</ItemGroup>
```

## Usage

### Enable ConstExpr

Add this to your `.csproj`:

```xml
<PropertyGroup>
  <UseConstExpr>true</UseConstExpr>
</PropertyGroup>
```

### Mark Methods for Optimization

Use the `[ConstExpr]` attribute on methods you want to optimize:

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

### Use with Constant Values

When you call these methods with constant arguments, ConstExpr optimizes them at compile time:

```csharp
// Your code:
var sum = MathOperations.Add(10.5, 20.3);
var isPrime = MathOperations.IsPrime(17);

// Generated at compile time:
var sum = 30.8;
var isPrime = true;
```

## Advanced Configuration

### Floating-Point Modes

Control floating-point evaluation behavior:

```csharp
// Strict IEEE 754 compliance (default)
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.Strict)]
public static class StrictMath
{
    // Guarantees exact IEEE 754 behavior
}

// Fast-math optimizations
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class FastMath
{
    // Allows optimizations that may deviate slightly from IEEE 754
}
```

### String Operations Example

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

// Evaluated at compile time when called with constants
var name = StringOps.FormatFullName("John", "Doe");
var length = StringOps.StringLength("Hello", Encoding.UTF8);
```

## How It Works

The source generator follows a 5-step optimization process:

1. **Detection**: Scans for method invocations marked with `[ConstExpr]`
2. **Analysis**: Determines if the method can be evaluated at compile time
3. **Evaluation**: Executes the method during compilation with constant arguments
4. **Generation**: Replaces the method call with the computed constant value
5. **Caching**: Stores results for incremental compilation

## Limitations

- Only methods with constant arguments can be evaluated at compile time
- Some operations may not be fully evaluable due to runtime dependencies
- Complex dynamic behavior cannot be optimized

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This project is licensed under the MIT License.

## Author

Jan Tamis ([GitHub](https://github.com/JanTamis))

## Links

- [NuGet Package](https://www.nuget.org/packages/ConstExpr.SourceGenerator)
- [GitHub Repository](https://github.com/JanTamis/ConstExpr)
