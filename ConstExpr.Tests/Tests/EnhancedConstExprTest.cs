using ConstExpr.SourceGenerator.Operations;

namespace ConstExpr.Tests.Tests;

public class EnhancedPatternMatchingTest : BaseTest<string>
{
    public override IEnumerable<string> Result => [ "Pattern matched successfully", "List pattern works", "Property pattern works" ];

    public override string SourceCode => """
        using System.Collections.Generic;
        using ConstantExpression;

        namespace Testing;

        public static class Classes
        {
            public void Test()
            {
                Test.TestPatterns();
            }

            [ConstExpr]
            public static IEnumerable<string> TestPatterns()
            {
                var results = new List<string>();
                
                // Enhanced pattern matching examples
                var value = 42;
                
                // Relational pattern
                if (value is > 40 and < 50)
                {
                    results.Add("Pattern matched successfully");
                }
                
                // List pattern (conceptual - would need language support)
                var list = new[] { 1, 2, 3 };
                if (list.Length == 3)
                {
                    results.Add("List pattern works");
                }
                
                // Property pattern (conceptual)
                var obj = new { Name = "Test", Age = 25 };
                if (obj.Name == "Test")
                {
                    results.Add("Property pattern works");
                }
                
                return results;
            }
        }
        """;
}

public class AdvancedLinqTest : BaseTest<string>
{
    public override IEnumerable<string> Result => [ "GroupBy: 2 groups", "Join: 2 matches", "Advanced aggregation" ];

    public override string SourceCode => """
        using System.Collections.Generic;
        using System.Linq;
        using ConstantExpression;

        namespace Testing;

        public static class Classes
        {
            public void Test()
            {
                Test.TestAdvancedLinq();
            }

            [ConstExpr]
            public static IEnumerable<string> TestAdvancedLinq()
            {
                var results = new List<string>();
                
                // Advanced GroupBy
                var numbers = new[] { 1, 2, 3, 4, 5, 6 };
                var groups = numbers.GroupBy(x => x % 2);
                results.Add($"GroupBy: {groups.Count()} groups");
                
                // Advanced Join
                var names = new[] { "Alice", "Bob" };
                var ages = new[] { (Name: "Alice", Age: 25), (Name: "Bob", Age: 30) };
                var joined = names.Join(ages, n => n, a => a.Name, (n, a) => $"{n} is {a.Age}");
                results.Add($"Join: {joined.Count()} matches");
                
                // Advanced aggregation
                var sum = numbers.Aggregate(0, (acc, x) => acc + x);
                results.Add($"Advanced aggregation");
                
                return results;
            }
        }
        """;
}

public class MathematicalExtensionsTest : BaseTest<double>
{
    public override IEnumerable<double> Result => [ 3.5, 4.0, 1.58, 2.5 ];

    public override string SourceCode => """
        using System.Collections.Generic;
        using System.Linq;
        using ConstantExpression;

        namespace Testing;

        public static class Classes
        {
            public void Test()
            {
                Test.TestMathematicalExtensions();
            }

            [ConstExpr]
            public static IEnumerable<double> TestMathematicalExtensions()
            {
                var data = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
                
                var results = new List<double>();
                
                // Calculate median
                var sortedData = data.OrderBy(x => x).ToArray();
                var median = (sortedData[2] + sortedData[3]) / 2.0;
                results.Add(median);
                
                // Calculate mode (simplified)
                var mode = data.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
                results.Add(mode);
                
                // Calculate standard deviation (simplified)
                var mean = data.Average();
                var variance = data.Select(x => (x - mean) * (x - mean)).Average();
                var stdDev = System.Math.Sqrt(variance);
                results.Add(stdDev);
                
                // Calculate percentile (simplified)
                var percentile50 = sortedData[data.Length / 2 - 1];
                results.Add(percentile50);
                
                return results;
            }
        }
        """;
}

public class AdvancedStringProcessingTest : BaseTest<string>
{
    public override IEnumerable<string> Result => [ "HELLO", "hello", "Found match", "Split successful" ];

    public override string SourceCode => """
        using System.Collections.Generic;
        using System.Linq;
        using System.Text.RegularExpressions;
        using System.Globalization;
        using ConstantExpression;

        namespace Testing;

        public static class Classes
        {
            public void Test()
            {
                Test.TestAdvancedStringProcessing();
            }

            [ConstExpr]
            public static IEnumerable<string> TestAdvancedStringProcessing()
            {
                var results = new List<string>();
                var input = "Hello World";
                
                // Culture-specific operations
                results.Add(input.ToUpper(CultureInfo.InvariantCulture));
                results.Add(input.ToLower(CultureInfo.InvariantCulture));
                
                // Regex operations (simplified)
                var pattern = @"\b\w+\b";
                var regex = new Regex(pattern);
                if (regex.IsMatch(input))
                {
                    results.Add("Found match");
                }
                
                // String splitting
                var parts = input.Split(' ');
                if (parts.Length == 2)
                {
                    results.Add("Split successful");
                }
                
                return results;
            }
        }
        """;
}

public class PerformanceOptimizationsTest : BaseTest<int>
{
    public override IEnumerable<int> Result => [ 15, 21, 8, 100 ];

    public override string SourceCode => """
        using System.Collections.Generic;
        using System.Linq;
        using ConstantExpression;

        namespace Testing;

        public static class Classes
        {
            public void Test()
            {
                Test.TestPerformanceOptimizations();
            }

            [ConstExpr]
            public static IEnumerable<int> TestPerformanceOptimizations()
            {
                var results = new List<int>();
                
                // Loop unrolling simulation
                var data = new[] { 1, 2, 3, 4, 5 };
                
                // Unrolled sum
                var sum = data[0] + data[1] + data[2] + data[3] + data[4];
                results.Add(sum);
                
                // Vectorization simulation (manual SIMD-style operations)
                var a = new[] { 1, 2, 3, 4 };
                var b = new[] { 5, 6, 7, 8 };
                var vectorSum = (a[0] + b[0]) + (a[1] + b[1]) + (a[2] + b[2]) + (a[3] + b[3]);
                results.Add(vectorSum);
                
                // Memory layout optimization (sequential access)
                var count = 0;
                for (var i = 0; i < data.Length; i++)
                {
                    count += data[i] > 2 ? 1 : 0;
                }
                results.Add(count);
                
                // Compile-time constant folding
                const int factor = 10;
                const int base_value = 10;
                var optimized = factor * base_value;
                results.Add(optimized);
                
                return results;
            }
        }
        """;
}