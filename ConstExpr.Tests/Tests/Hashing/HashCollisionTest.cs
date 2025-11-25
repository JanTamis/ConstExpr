using Microsoft.CodeAnalysis.CSharp;
using ConstExpr.SourceGenerator.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.Tests.Tests.Hashing;

public class HashCollisionTest
{
  public static void RunCollisionAnalysis()
  {
    var visitor = new DeteministicHashVisitor();
    var hashes = new Dictionary<ulong, List<string>>();
    int totalSamples = 0;

    var testCases = GenerateTestCases().ToList();
    Console.WriteLine($"FNV-1a Hash Collision Analysis");
    Console.WriteLine($"Testing {testCases.Count} code samples...\n");

    foreach (var code in testCases)
    {
      try
      {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var hash = visitor.Visit(root);

        if (!hashes.ContainsKey(hash))
        {
          hashes[hash] = new List<string>();
        }
        hashes[hash].Add(code);
        totalSamples++;
      }
      catch
      {
        // Skip invalid code
      }
    }

    // Calculate statistics
    var collisions = hashes.Where(kvp => kvp.Value.Count > 1).ToList();
    var uniqueHashes = hashes.Count;
    var collisionCount = collisions.Sum(c => c.Value.Count - 1);
    var collisionRate = totalSamples > 0 ? (double)collisionCount / totalSamples : 0.0;

    // Print results
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║     COLLISION ANALYSIS RESULTS                ║");
    Console.WriteLine("╠═══════════════════════════════════════════════╣");
    Console.WriteLine($"║ Total samples tested:  {totalSamples,20} ║");
    Console.WriteLine($"║ Unique hashes:         {uniqueHashes,20} ║");
    Console.WriteLine($"║ Total collisions:      {collisionCount,20} ║");
    Console.WriteLine($"║ Collision rate:        {collisionRate * 100,19:F6}% ║");
    Console.WriteLine($"║ Hash utilization:      {(double)uniqueHashes / totalSamples,19:P2} ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝");
    Console.WriteLine();

    // Bit distribution analysis
    var bitCounts = new int[64];
    foreach (var hash in hashes.Keys)
    {
      for (int i = 0; i < 64; i++)
      {
        if ((hash & (1UL << i)) != 0)
          bitCounts[i]++;
      }
    }

    var expectedCount = uniqueHashes / 2.0;
    var chiSquare = bitCounts.Sum(count => System.Math.Pow(count - expectedCount, 2) / expectedCount);
    var avgBitDensity = bitCounts.Average() / uniqueHashes;
    
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║     BIT DISTRIBUTION ANALYSIS                ║");
    Console.WriteLine("╠═══════════════════════════════════════════════╣");
    Console.WriteLine($"║ Chi-Square value:      {chiSquare,20:F2} ║");
    Console.WriteLine($"║   (expected ~64, lower = better)             ║");
    Console.WriteLine($"║ Average bit density:   {avgBitDensity,19:P2} ║");
    Console.WriteLine($"║   (expected ~50%)                            ║");
    Console.WriteLine($"║ Min bit usage:         {bitCounts.Min(),20} ║");
    Console.WriteLine($"║ Max bit usage:         {bitCounts.Max(),20} ║");
    Console.WriteLine($"║ Bit usage range:       {bitCounts.Max() - bitCounts.Min(),20} ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝");
    Console.WriteLine();

    // Show collisions if any
    if (collisions.Any())
    {
      Console.WriteLine($"⚠ COLLISION DETAILS (Top {System.Math.Min(10, collisions.Count)}):");
      Console.WriteLine();
      foreach (var collision in collisions.OrderByDescending(c => c.Value.Count).Take(10))
      {
        Console.WriteLine($"Hash: 0x{collision.Key:X16} - {collision.Value.Count} items:");
        foreach (var code in collision.Value)
        {
          var preview = code.Replace("\n", " ").Replace("\r", "").Trim();
          if (preview.Length > 70)
            preview = preview.Substring(0, 67) + "...";
          Console.WriteLine($"  • {preview}");
        }
        Console.WriteLine();
      }
    }
    else
    {
      Console.WriteLine("✓✓✓ NO COLLISIONS DETECTED ✓✓✓");
      Console.WriteLine("    Perfect hash distribution!");
      Console.WriteLine();
    }

    // Quality assessment
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║     QUALITY ASSESSMENT                       ║");
    Console.WriteLine("╠═══════════════════════════════════════════════╣");
    
    if (collisionRate == 0)
    {
      Console.WriteLine("║ ✓ PERFECT: No collisions detected           ║");
    }
    else if (collisionRate < 0.0001)
    {
      Console.WriteLine("║ ✓ EXCELLENT: Collision rate < 0.01%         ║");
    }
    else if (collisionRate < 0.001)
    {
      Console.WriteLine("║ ✓ GOOD: Collision rate < 0.1%               ║");
    }
    else if (collisionRate < 0.01)
    {
      Console.WriteLine("║ ⚠ ACCEPTABLE: Collision rate < 1%           ║");
    }
    else
    {
      Console.WriteLine("║ ✗ POOR: High collision rate >= 1%           ║");
      Console.WriteLine("║   Consider improving hash algorithm          ║");
    }

    if (chiSquare >= 40 && chiSquare <= 90)
    {
      Console.WriteLine("║ ✓ GOOD: Chi-square in acceptable range      ║");
    }
    else if (chiSquare < 40)
    {
      Console.WriteLine("║ ⚠ Chi-square too low (too uniform)          ║");
    }
    else
    {
      Console.WriteLine("║ ⚠ Chi-square too high (poor distribution)   ║");
    }

    if (System.Math.Abs(avgBitDensity - 0.5) < 0.05)
    {
      Console.WriteLine("║ ✓ GOOD: Bit density near optimal 50%        ║");
    }
    else
    {
      Console.WriteLine("║ ⚠ Bit density deviation from optimal        ║");
    }

    Console.WriteLine("╚═══════════════════════════════════════════════╝");
  }

  private static IEnumerable<string> GenerateTestCases()
  {
    // Test 1: Simple arithmetic with variations
    for (int i = 0; i < 200; i++)
    {
      yield return $"{i} + 1";
      yield return $"1 + {i}";
      yield return $"{i} * 2";
      yield return $"x + {i}";
    }

    // Test 2: Variable declarations
    for (int i = 0; i < 150; i++)
    {
      yield return $"var x{i} = {i};";
      yield return $"int value{i} = {i};";
      yield return $"string s{i} = \"test{i}\";";
    }

    // Test 3: Method calls
    for (int i = 0; i < 100; i++)
    {
      yield return $"Method{i}();";
      yield return $"Func{i}(arg);";
      yield return $"obj.Call{i}(param{i});";
    }

    // Test 4: Binary operations with all operators
    var ops = new[] { "+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "==", "!=", "<", ">", "<=", ">=" };
    for (int i = 0; i < 50; i++)
    {
      foreach (var op in ops)
      {
        yield return $"x {op} {i}";
        yield return $"{i} {op} y";
      }
    }

    // Test 5: Control flow
    for (int i = 0; i < 80; i++)
    {
      yield return $"if (x > {i}) return {i};";
      yield return $"while (x < {i}) x++;";
      yield return $"for (int j = 0; j < {i}; j++) {{ }}";
      yield return $"foreach (var item in collection{i}) {{ }}";
    }

    // Test 6: Property and field access
    for (int i = 0; i < 100; i++)
    {
      yield return $"obj.Property{i}";
      yield return $"this.Field{i}";
      yield return $"instance.Value{i}";
    }

    // Test 7: String literals (collision-prone)
    for (int i = 0; i < 100; i++)
    {
      yield return $"\"{i}\"";
      yield return $"\"test{i}\"";
      yield return $"\"value_{i}\"";
    }

    // Test 8: Array operations
    for (int i = 0; i < 80; i++)
    {
      yield return $"array[{i}]";
      yield return $"new int[{i}]";
      yield return $"new[] {{ {i}, {i + 1} }}";
    }

    // Test 9: Lambda expressions
    for (int i = 0; i < 60; i++)
    {
      yield return $"x => x + {i}";
      yield return $"() => {i}";
      yield return $"(a, b) => a * {i} + b";
    }

    // Test 10: Complex nested expressions
    for (int i = 0; i < 50; i++)
    {
      yield return $"(x + {i}) * (y - {i})";
      yield return $"array[{i}] + array[{i + 1}] * {i}";
      yield return $"x * {i} + y / {i} - z % {i}";
    }

    // Test 11: Type declarations
    for (int i = 0; i < 50; i++)
    {
      yield return $"int x{i};";
      yield return $"List<int> list{i};";
      yield return $"Dictionary<string, int> dict{i};";
    }

    // Test 12: Pattern matching
    for (int i = 0; i < 50; i++)
    {
      yield return $"x is {i}";
      yield return $"x is > {i}";
      yield return $"x is < {i} and > 0";
    }

    // Test 13: Switch expressions
    for (int i = 0; i < 40; i++)
    {
      yield return $"x switch {{ {i} => true, _ => false }}";
      yield return $"value switch {{ {i} => {i}, _ => 0 }}";
    }

    // Test 14: Numeric literals in different formats
    for (int i = 0; i < 50; i++)
    {
      yield return $"{i}";
      yield return $"{i}L";
      yield return $"{i}U";
      yield return $"{i}.0";
      yield return $"{i}f";
    }

    // Test 15: Similar variable names (high collision risk)
    for (int i = 0; i < 50; i++)
    {
      yield return $"variable{i}";
      yield return $"Variable{i}";
      yield return $"VARIABLE{i}";
      yield return $"_variable{i}";
    }
  }
}
