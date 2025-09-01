using ConstExpr.SourceGenerator.Sample;
using System;
using System.Text;

var range = Test.Range(10);

Console.WriteLine(String.Join(", ", Test.IsOdd(1, 2, 3, 4, 5)));
Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f, 6f));
Console.WriteLine(Test.StdDev(1f, 2f, 3f, 4f, 5f));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
Console.WriteLine(Test.StringBytes("Hello, World!", Encoding.UTF8).Length);
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(await Test.Waiting());
// Console.WriteLine(String.Join(", ", range.BinarySearch(11, Comparer<int>.Default)));
Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
Console.WriteLine(Test.RgbToHsl(150, 100, 50));
Console.WriteLine(Test.IsPrime(4));
Console.WriteLine(Test.ToString(StringComparison.Ordinal));
Console.WriteLine(Test.GetNames<StringComparison>());
Console.WriteLine(Test.GetNames<StringComparison>());
Console.WriteLine(Test.GetNames<StringSplitOptions>());
Console.WriteLine(Test.GetArray(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));
Console.WriteLine(Test.InterpolationTest("Test", 42, 3.14));
//
// AdvancedPrimeTest demo
// Console.WriteLine(Test.AdvancedPrimeTest(100));

// Numbers / sequences
Console.WriteLine("Primes up to 50: " + String.Join(", ", Test.PrimesUpTo(50)));
Console.WriteLine("First 12 Fibonacci (long): " + String.Join(", ", Test.FibonacciSequence(12)));
Console.WriteLine("Clamp(15, 0, 10) => " + Test.Clamp(15, 0, 10));
Console.WriteLine("Map(5, 0..10 -> 0..100) => " + Test.Map(5, 0, 10, 0, 100));

// Color conversions
var (h, s, l) = Test.RgbToHsl((byte)Math.Abs(-150), 100, 50);
Console.WriteLine($"RGB(150,100,50) -> HSL({h:F1}, {s:F3}, {l:F3})");

var (rr, gg, bb) = Test.HslToRgb(h, s, l);
Console.WriteLine($"Round-trip HSL -> RGB({rr},{gg},{bb})");

var lumDark = Test.Luminance(0, 128, 0);
var lumLight = Test.Luminance(255, 255, 255);

Console.WriteLine($"Luminance black={lumDark:F4} white={lumLight:F4}");
Console.WriteLine("Contrast black/white: " + Test.ContrastRatio(0, 0, 0, 255, 255, 255).ToString("F2"));

// A couple of pre-existing samples to keep context
Console.WriteLine("IsPrime(97) => " + Test.IsPrime(97));
Console.WriteLine("StdDev(1..5) => " + Test.StdDev(1, 2, 3, 4, 5));

