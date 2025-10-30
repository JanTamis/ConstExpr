# ConstExpr

A C# source generator that performs compile-time constant expression evaluation and optimization, transforming runtime computations into compile-time constants when possible.

## Overview

ConstExpr is a Roslyn-based source generator that analyzes your C# code to detect method calls and expressions that can be evaluated at compile time. When it finds such opportunities, it automatically generates optimized code with pre-computed constant values, eliminating runtime overhead.

## Features

- **Compile-Time Evaluation**: Automatically evaluates constant expressions at compile time
- **Performance Optimization**: Reduces runtime overhead by pre-computing values during compilation
- **Floating-Point Modes**: Support for both strict IEEE 754 and fast-math optimizations
- **Comprehensive Support**: Works with various operation types:
  - Mathematical operations (averages, standard deviation, prime calculations)
  - String operations (formatting, encoding, interpolation)
  - Collection operations (LINQ queries, transformations)
  - Date/Time calculations
  - Geometry and physics calculations
  - Financial computations
  - Color operations
- **Incremental Generation**: Efficient caching prevents unnecessary reprocessing
- **Seamless Integration**: Works transparently with existing C# code

## Installation

Install via NuGet:

```bash
dotnet add package ConstExpr.SourceGenerator
```

Or add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="ConstExpr.SourceGenerator" Version="0.1.19-preview" />
</ItemGroup>
```

## Usage

### Basic Setup

1. Enable ConstExpr in your project by adding to your `.csproj`:

```xml
<PropertyGroup>
  <UseConstExpr>true</UseConstExpr>
</PropertyGroup>
```

2. Mark methods for constant expression evaluation using the `[ConstExpr]` attribute:

```csharp
using ConstExpr.Core.Attributes;

[ConstExpr]
public static class MathOperations
{
    public static double Average(params IReadOnlyList<double> data)
    {
        return data.Average();
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

3. Use the methods with constant values - the source generator will optimize them:

```csharp
// This will be evaluated at compile time
var average = MathOperations.Average(1, 2, 3, 4, 5); // Result: 3.0
var isPrime = MathOperations.IsPrime(17); // Result: true
```

### Floating-Point Evaluation Modes

You can control floating-point semantics using the `FloatingPointMode` property:

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
    // Allows optimizations that may slightly deviate from IEEE 754
}
```

### Example: String Operations

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

// At compile time:
var name = StringOps.FormatFullName("John", "Doe"); // "John Doe"
var length = StringOps.StringLength("Hello", Encoding.UTF8); // 5
```

## Building

### Prerequisites

- .NET SDK 6.0 or later
- Visual Studio 2022 or JetBrains Rider (optional)

### Build Instructions

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build ConstExpr.sln

# Build in Release mode
dotnet build ConstExpr.sln -c Release
```

### Running Tests

```bash
dotnet test ConstExpr.sln
```

## Benchmarking

ConstExpr uses [BenchmarkDotNet](https://benchmarkdotnet.org/) for performance testing.

### Running Benchmarks Locally

```bash
dotnet run -c Release --project Benchmarks/IntSqrtBench/IntSqrtBench.csproj
```

### Running All Benchmarks

```bash
dotnet run -c Release --project Benchmarks/IntSqrtBench/IntSqrtBench.csproj --filter '*'
```

## Project Structure

```
ConstExpr/
├── Vectorize/
│   ├── ConstExpr.SourceGenerator/  # Main source generator implementation
│   ├── ConstExpr.Core/             # Core attributes and types
│   └── ConstExpr.Sample/           # Sample implementations and examples
├── ConstExpr.Tests/                # Unit tests
├── Benchmarks/                     # Performance benchmarks
│   └── IntSqrtBench/              # Integer square root benchmarks
└── SourceGen.Utilities/           # Source generation utilities
```

## How It Works

1. **Detection**: The source generator scans for invocations of methods marked with `[ConstExpr]`
2. **Analysis**: It analyzes whether the method can be evaluated at compile time (all arguments are constants)
3. **Evaluation**: If eligible, the method is executed during compilation
4. **Generation**: Optimized code is generated with the pre-computed constant value
5. **Caching**: Results are cached using incremental generation to avoid redundant processing

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
