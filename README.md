# ConstExpr ⚡

> **Transform runtime computations into compile-time constants automatically!**

Why wait until runtime to calculate values that never change? ConstExpr is a powerful C# source generator that automatically detects and evaluates constant expressions at compile time, turning expensive runtime computations into zero-cost constants.

```csharp
// You write this:
var result = MathOps.IsPrime(17);

// ConstExpr generates this at compile time:
var result = true;  // Computed during compilation - zero runtime cost!
```

## 🚀 What Makes ConstExpr Special?

ConstExpr is a Roslyn-based source generator that acts like a **compile-time optimizer on steroids**. It analyzes your code, identifies opportunities for constant folding, and automatically generates optimized code with pre-computed values - eliminating runtime overhead entirely.

**The result?** Faster applications, smaller binaries, and the same clean, readable code you're already writing.

## ✨ Features

- 🎯 **Zero-Cost Abstractions**: Write clean, expressive code without runtime performance penalties
- ⚡ **Automatic Optimization**: No manual intervention needed - just add an attribute
- 🔬 **Floating-Point Precision Control**: Choose between strict IEEE 754 compliance or fast-math optimizations
- 🎨 **Extensive Operation Support**:
  - 📊 Mathematical operations (statistics, prime detection, numeric algorithms)
  - 📝 String manipulation (formatting, encoding, text processing)
  - 📦 Collection operations (LINQ queries, transformations, filtering)
  - 📅 Date/Time calculations
  - 📐 Geometry and physics computations
  - 💰 Financial calculations
  - 🎨 Color transformations
- 🔄 **Incremental Generation**: Smart caching means blazing-fast rebuilds
- 🔌 **Drop-in Integration**: Works seamlessly with your existing codebase

## 📦 Installation

Get started in seconds via NuGet:

```bash
dotnet add package ConstExpr.SourceGenerator
```

Or add directly to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="ConstExpr.SourceGenerator" Version="0.1.19-preview" />
</ItemGroup>
```

## 💡 Quick Start

### Step 1: Enable ConstExpr

Add this to your `.csproj`:

```xml
<PropertyGroup>
  <UseConstExpr>true</UseConstExpr>
</PropertyGroup>
```

### Step 2: Mark Your Methods

Decorate methods with `[ConstExpr]` to unlock compile-time magic:

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

### Step 3: Watch the Magic Happen! ✨

Use your methods with constant values - ConstExpr does the rest:

```csharp
// Your code:
var sum = MathOperations.Add(10.5, 20.3);
var isPrime = MathOperations.IsPrime(17);

// Generated at compile time:
var sum = 30.8;      // No runtime calculation!
var isPrime = true;  // Already computed!
```

**The performance benefit?** These operations now have literally **zero runtime cost** - they're just constants baked into your assembly! 🎉

## 🎛️ Advanced Configuration

### Floating-Point Precision Control

Choose the right balance between speed and precision:

```csharp
// ⚖️ Strict mode: Guarantees exact IEEE 754 behavior (default)
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.Strict)]
public static class StrictMath
{
    // Perfect precision, every time
}

// 🏎️ Fast-math mode: Maximum performance with acceptable tradeoffs
[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class FastMath
{
    // Blazing fast, may slightly deviate from IEEE 754
}
```

### More Examples

**String Operations - Computed at Compile Time:**

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

// Look ma, no runtime overhead!
var name = StringOps.FormatFullName("John", "Doe");      // Compile-time: "John Doe"
var length = StringOps.StringLength("Hello", Encoding.UTF8);  // Compile-time: 5
```

## 🔍 How It Works

ConstExpr performs its magic through a carefully orchestrated 5-step process:

1. 🔎 **Detection**: Scans your code for methods decorated with `[ConstExpr]`
2. 🧪 **Analysis**: Determines if calls can be evaluated at compile time (are all arguments constants?)
3. ⚙️ **Evaluation**: Executes the method during compilation with the constant values
4. 🎯 **Generation**: Replaces the method call with the pre-computed constant result
5. 💾 **Caching**: Stores results using incremental generation for lightning-fast rebuilds

The end result? Your complex logic runs once at compile time, then becomes a simple constant forever!

## ⚠️ What to Know

ConstExpr is powerful, but it's not magic - here's what you should keep in mind:

- ✅ **Works with**: Methods called with constant arguments
- ❌ **Can't optimize**: Runtime dependencies, dynamic behavior, non-constant inputs
- 💡 **Best for**: Configuration values, mathematical constants, pre-computed lookup tables

## 🤝 Contributing

Contributions are welcome! Found a bug? Have an idea? Feel free to open an issue or submit a pull request.

## 📄 License

MIT License - use it, modify it, share it!

## 👨‍💻 Author

**Jan Tamis** - [GitHub](https://github.com/JanTamis)

## 🔗 Links

- 📦 [NuGet Package](https://www.nuget.org/packages/ConstExpr.SourceGenerator)
- 💻 [GitHub Repository](https://github.com/JanTamis/ConstExpr)

---

<div align="center">

**Made with ❤️ for the C# community**

*Star ⭐ this repo if you find it useful!*

</div>
