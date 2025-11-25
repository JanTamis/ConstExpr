using Microsoft.CodeAnalysis.CSharp;
using ConstExpr.SourceGenerator.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ConstExpr.Tests.Tests.Hashing;

public class RealisticHashCollisionTest
{
  private readonly ITestOutputHelper _output;

  public RealisticHashCollisionTest(ITestOutputHelper output)
  {
    _output = output;
  }

  [Fact]
  public void TestHashCollisionWithRealisticCode()
  {
    var visitor = new DeteministicHashVisitor();
    var hashes = new Dictionary<ulong, List<string>>();
    int totalSamples = 0;

    var testCases = GenerateRealisticTestCases().ToList();
    _output.WriteLine($"Testing {testCases.Count} realistic code samples...\n");

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
    _output.WriteLine("═══ REALISTIC CODE COLLISION ANALYSIS ═══");
    _output.WriteLine($"Total samples tested:  {totalSamples}");
    _output.WriteLine($"Unique hashes:         {uniqueHashes}");
    _output.WriteLine($"Total collisions:      {collisionCount}");
    _output.WriteLine($"Collision rate:        {collisionRate:P4} ({collisionRate * 100:F6}%)");
    _output.WriteLine($"Hash utilization:      {(double)uniqueHashes / totalSamples:P2}");
    _output.WriteLine("");

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
    
    _output.WriteLine("═══ BIT DISTRIBUTION ═══");
    _output.WriteLine($"Chi-Square value:      {chiSquare:F2}");
    _output.WriteLine($"Average bit density:   {bitCounts.Average() / uniqueHashes:P2}");
    _output.WriteLine("");

    // Show collisions if any
    if (collisions.Any())
    {
      _output.WriteLine($"⚠ Found {collisions.Count} collision groups:");
      foreach (var collision in collisions.Take(5))
      {
        _output.WriteLine($"\nHash: 0x{collision.Key:X16} ({collision.Value.Count} items)");
        foreach (var code in collision.Value.Take(3))
        {
          var preview = code.Replace("\n", " ").Trim();
          if (preview.Length > 80)
            preview = preview.Substring(0, 77) + "...";
          _output.WriteLine($"  • {preview}");
        }
      }
    }
    else
    {
      _output.WriteLine("✓ NO COLLISIONS - Perfect!");
    }

    // Assertions
    Assert.True(collisionRate < 0.01, $"Collision rate {collisionRate:P2} is too high (should be < 1%)");
    Assert.InRange(chiSquare, 30, 100); // Reasonable range for Chi-square
  }

  private static IEnumerable<string> GenerateRealisticTestCases()
  {
    // Complete method bodies
    for (int i = 0; i < 100; i++)
    {
      yield return @"
public int Calculate" + i + @"(int x, int y) 
{ 
  var result = x + y * " + i + @"; 
  return result; 
}";

      yield return @"
public void Process" + i + @"() 
{ 
  for (int j = 0; j < " + i + @"; j++) 
  { 
    Console.WriteLine(j); 
  } 
}";

      yield return @"
public string GetValue" + i + @"(string input)
{
  if (input == null) return string.Empty;
  return input.Substring(0, System.Math.Min(input.Length, " + i + @"));
}";
    }

    // Property declarations
    for (int i = 0; i < 100; i++)
    {
      yield return $"public int Property{i} {{ get; set; }} = {i};";
      yield return $"private string _field{i} = \"value{i}\";";
      yield return $"public readonly double Constant{i} = {i}.0;";
    }

    // Complex expressions
    for (int i = 0; i < 100; i++)
    {
      yield return $"var result{i} = (x * {i} + y) / (z - {i});";
      yield return $"var value{i} = array[{i}] + array[{i + 1}] * factor;";
      yield return $"return x > {i} ? x * {i} : x / {i};";
    }

    // Lambda expressions in context
    for (int i = 0; i < 100; i++)
    {
      yield return $"Func<int, int> func{i} = x => x * {i} + {i};";
      yield return $"var filtered{i} = items.Where(x => x.Value > {i}).ToList();";
      yield return $"var mapped{i} = data.Select(x => x * {i}).Sum();";
    }

    // Control flow statements
    for (int i = 0; i < 100; i++)
    {
      yield return @"
if (value > " + i + @") 
{ 
  result = value * " + i + @"; 
} 
else 
{ 
  result = value + " + i + @"; 
}";

      yield return @"
switch (type" + i + @") 
{ 
  case " + i + @": return value" + i + @"; 
  case " + (i + 1) + @": return value" + (i + 1) + @"; 
  default: return 0; 
}";

      yield return @"
while (count < " + i + @") 
{ 
  count++; 
  total += count; 
}";
    }

    // Class declarations
    for (int i = 0; i < 50; i++)
    {
      yield return @"
public class MyClass" + i + @" 
{ 
  private int _value = " + i + @"; 
  public int GetValue() => _value * " + i + @"; 
}";

      yield return $"public record Person{i}(string Name, int Age{i});";

      yield return @"
public interface IService" + i + @" 
{ 
  Task<int> Execute" + i + @"Async(int param" + i + @"); 
}";
    }

    // LINQ queries
    for (int i = 0; i < 50; i++)
    {
      yield return @"
var query" + i + @" = from x in items" + i + @"
               where x.Value > " + i + @"
               select x.Name" + i + @";";

      yield return $"var result{i} = items.GroupBy(x => x.Category{i}).Select(g => g.Count()).Sum();";
    }

    // Pattern matching - using verbatim strings with proper escaping
    for (int i = 0; i < 50; i++)
    {
      yield return $"var isValid{i} = obj is {{ Value: > {i}, Status{i}: not null }};";
      yield return $"return value switch {{ < {i} => Low{i}, > {i * 2} => High{i}, _ => Normal{i} }};";
    }

    // Array and collection initialization
    for (int i = 0; i < 50; i++)
    {
      yield return $"var array{i} = new int[] {{ {i}, {i + 1}, {i + 2}, {i + 3} }};";
      yield return $"var list{i} = new List<int> {{ {i}, {i * 2}, {i * 3} }};";
      yield return $"var dict{i} = new Dictionary<string, int> {{ [\"key{i}\"] = {i} }};";
    }

    // String interpolation - use double curly braces
    for (int i = 0; i < 50; i++)
    {
      yield return "var message" + i + " = $\"Value is {value" + i + "} at index " + i + "\";";
      yield return "Console.WriteLine($\"Processing item " + i + ": {item.Name" + i + "}\");";
    }

    // Async/await patterns
    for (int i = 0; i < 50; i++)
    {
      yield return @"
public async Task<int> GetDataAsync" + i + @"()
{
  var data = await FetchAsync" + i + @"();
  return data.Length * " + i + @";
}";
    }
  }
}
