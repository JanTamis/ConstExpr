using ConstExpr.SourceGenerator.Sample;
using System;
using System.Text;

// var range = Test.Range(10);

float test = 1f;
byte test2 = 128;

// Console.WriteLine(String.Join(", ", Test.Range(5)));

// Console.WriteLine(String.Join(", ", Test.IsOdd(1, 2, 3, 4, 5)));
// Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f, 6f));
Console.WriteLine(Test.StdDev(test, 2f, 3f, 4f, 5f));
//
// Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
// Console.WriteLine(Test.StringBytes("Hello, World!!!", Encoding.UTF8).Length);
// Console.WriteLine(Test.Base64Encode("Hello, World!"));
// Console.WriteLine(await Test.Waiting());
// Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
// Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
// Console.WriteLine(Test.RgbToHsl(test2, 100, 50));
// Console.WriteLine(Test.IsPrime(3));
// Console.WriteLine(Test.ToString(StringComparison.Ordinal));
// Console.WriteLine(Test.GetNames<StringComparison>());
// Console.WriteLine(Test.GetNames<StringComparison>());
// Console.WriteLine(Test.GetNames<StringSplitOptions>());
// Console.WriteLine(Test.GetArray(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));
// Console.WriteLine(Test.InterpolationTest("Test", 42, 3.14));
//
// // Numbers / sequences
// Console.WriteLine("Primes up to 50: " + String.Join(", ", Test.PrimesUpTo(5)));
// Console.WriteLine("First 12 Fibonacci (long): " + String.Join(", ", Test.FibonacciSequence(12)));
// Console.WriteLine("Clamp(15, 0, 10) => " + Test.Clamp(20, test2, test2));
// Console.WriteLine("Map(5, 0..10 -> 0..100) => " + Test.Map(5, 0, 10, 0, 100));
//
// // Color conversions
// var (h, s, l) = Test.RgbToHsl(150, test2, 50);
// Console.WriteLine($"RGB(150,100,50) -> HSL({h:F1}, {s:F3}, {l:F3})");
//
// var (rr, gg, bb) = Test.HslToRgb(720, test, 0.5f);
// Console.WriteLine($"Round-trip HSL -> RGB({rr},{gg},{bb})");
//
// var lumDark = Test.Luminance(0, test2, test2);
// var lumLight = Test.Luminance(255, 255, 255);
//
// Console.WriteLine($"Luminance black={lumDark:F4} white={lumLight:F4}");
// Console.WriteLine("Contrast black/white: " + Test.ContrastRatio(0, 0, 0, 255, 255, 255).ToString("F2"));
//
// // A couple of pre-existing samples to keep context
// Console.WriteLine("IsPrime(97) => " + Test.IsPrime(97));
// Console.WriteLine("StdDev(1..5) => " + Test.StdDev(1, 2, 3, 4, 5));

// // Demonstration of multi-parameter BlendRgb (gamma-correct and simple sRGB)
// var (br1, bg1, bb1) = Test.BlendRgb(255, 0, 0, 0, 0, 255, test, gammaCorrect: true);
// Console.WriteLine($"Gamma-correct blend red over blue @0.5 => RGB({br1},{bg1},{bb1})");
// var (br2, bg2, bb2) = Test.BlendRgb(255, 0, 0, 0, 0, 255, 0.5f, gammaCorrect: false);
// Console.WriteLine($"Simple sRGB blend red over blue @0.5 => RGB({br2},{bg2},{bb2})");