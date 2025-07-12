# ConstExpr - Compile-Time Expression Optimizer

ConstExpr is a powerful C# source generator that optimizes method calls at compile time, transforming runtime computations into compile-time constants for improved performance.

## 🚀 Features

- **Compile-Time Optimization**: Converts method calls into optimized, compile-time evaluated expressions
- **LINQ Support**: Optimizes LINQ operations like `Where`, `Average`, `Select`, and more
- **Mathematical Operations**: Compile-time evaluation of arithmetic, statistics, and mathematical functions
- **String Operations**: Optimized string manipulation, encoding, and processing
- **Collection Operations**: Enhanced performance for array and collection operations
- **Vector Operations**: Hardware-accelerated operations using `System.Numerics.Vector`
- **Configurable Generation Levels**: Choose between minimal, balanced, and performance optimization levels

## 📦 Installation

Add the ConstExpr source generator to your project:

```xml
<PackageReference Include="ConstExpr.SourceGenerator" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 🔧 Usage

### Basic Example

Mark your methods with the `[ConstExpr]` attribute to enable compile-time optimization:

```csharp
using ConstantExpression;
using System.Linq;

[ConstExpr]
public static class MathOperations
{
    public static double Average(params double[] values)
    {
        return values.Average();
    }
    
    public static IEnumerable<int> GetEvens(params int[] numbers)
    {
        return numbers.Where(x => x % 2 == 0);
    }
}

// Usage - these calls will be optimized at compile time
var avg = MathOperations.Average(1.0, 2.0, 3.0, 4.0, 5.0);
var evens = MathOperations.GetEvens(1, 2, 3, 4, 5, 6);
```

### Generation Levels

Control the optimization level using the `Level` property:

```csharp
[ConstExpr(Level = GenerationLevel.Performance)]
public static class HighPerformanceOps
{
    public static double StandardDeviation(params double[] data)
    {
        var sum = 0d;
        var sumOfSquares = 0d;

        foreach (var item in data)
        {
            sum += item;
            sumOfSquares += item * item;
        }

        var mean = sum / data.Length;
        var variance = sumOfSquares / data.Length - mean * mean;
        return Math.Sqrt(variance);
    }
}
```

**Generation Levels:**
- `Minimal`: Basic optimizations with minimal code generation
- `Balanced`: Default level with good balance of performance and code size
- `Performance`: Maximum optimization with potential for larger generated code

### Advanced Examples

#### String Operations
```csharp
[ConstExpr]
public static class StringOps
{
    public static int GetByteCount(string text, Encoding encoding)
    {
        return encoding.GetByteCount(text);
    }
    
    public static string EncodeBase64(string input)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }
}
```

#### Mathematical Computations
```csharp
[ConstExpr]
public static class Math
{
    public static IEnumerable<int> Fibonacci(int count)
    {
        if (count <= 0) yield break;
        
        int a = 0, b = 1;
        yield return a;
        
        for (int i = 1; i < count; i++)
        {
            yield return b;
            (a, b) = (b, a + b);
        }
    }
    
    public static (double H, double S, double L) RgbToHsl(int r, int g, int b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;
        
        // HSL conversion logic...
        // This will be computed at compile time!
    }
}
```

#### Async Operations
```csharp
[ConstExpr]
public static class AsyncOps
{
    public static async Task<string> ProcessDataAsync()
    {
        await Task.Delay(100);
        return "Processed";
    }
}
```

## 🏗️ Project Structure

```
ConstExpr/
├── Vectorize/
│   ├── ConstExpr.SourceGenerator/     # Main source generator implementation
│   │   ├── Analyzers/                 # Code analyzers
│   │   ├── Attributes/                # Diagnostic attributes
│   │   ├── Builders/                  # Code generation builders
│   │   ├── Enums/                     # Generation level enums
│   │   ├── Extensions/                # Helper extensions
│   │   └── Visitors/                  # Syntax visitors
│   └── ConstExpr.Sample/              # Example usage and benchmarks
└── ConstExpr.Tests/                   # Comprehensive test suite
```

## 🛠️ Requirements

- **.NET 9.0** or later
- **C# 13** or later (for latest language features)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)

## 🧪 Testing

The project includes comprehensive tests covering:

- LINQ operations optimization
- Mathematical computations
- String manipulation
- DateTime operations
- Complex nested expressions
- Performance benchmarks

Run tests using:
```bash
dotnet test
```

## 🎯 Performance Benefits

ConstExpr can provide significant performance improvements:

- **Compile-time evaluation**: Eliminates runtime computation overhead
- **Reduced allocations**: Optimized memory usage patterns
- **Vector operations**: Hardware-accelerated SIMD instructions
- **Loop unrolling**: Optimized iteration patterns

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🔗 Related Projects

- [System.Numerics.Vector](https://docs.microsoft.com/en-us/dotnet/api/system.numerics.vector) - Hardware acceleration
- [Roslyn Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) - Source generation framework

---

**Note**: This library leverages advanced C# compiler features and may require the latest .NET SDK for optimal performance and compatibility.