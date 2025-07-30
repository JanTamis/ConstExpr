using BenchmarkDotNet.Running;
using ConstExpr.SourceGenerator.Sample;
using ConstExpr.SourceGenerator.Sample.Tests;
using System;
using System.Text;

var range = Test.Range(10);

var test = 0;

Console.WriteLine(Test.IsOdd(1, 2, 3, 4, test));
Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.StdDev(1f, 2f, 3f, 4f, 5f));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
Console.WriteLine(Test.StringBytes("Hello, World!", Encoding.UTF8).Length);
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(await Test.Waiting());
// Console.WriteLine(String.Join(", ", range.BinarySearch(11, Comparer<int>.Default)));
// Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
Console.WriteLine(Test.RgbToHsl(150, 100, 50));

// Console.WriteLine(range.BinarySearch(2));

BenchmarkRunner.Run<ReplaceTest>();