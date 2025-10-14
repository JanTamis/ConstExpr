using ConstExpr.SourceGenerator.Sample;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

// var range = Test.Range(10);

float test = 1f;
byte test2 = 16;

//  Console.WriteLine(String.Join(", ", Test.Range(5)));
//
// Console.WriteLine(String.Join(", ", Test.IsOdd(1, 2, 3, 4, 5)));
// Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f, 6f));
// Console.WriteLine(Test.StdDev(test, 2f, 3f, 4f, 5f));
//
// Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
// Console.WriteLine(Test.StringBytes("Hello, World!!!", Encoding.UTF8).Length);
// Console.WriteLine(Test.Base64Encode("Hello, World!"));
// Console.WriteLine(await Test.Waiting());
// Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
// Console.WriteLine(Test.RgbToHsl(test2, 100, 50));
// Console.WriteLine(Test.IsPrime(3));
// Console.WriteLine(Test.IsPrime(test2));
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
// Console.WriteLine("\n=== NEW COMPLEX FUNCTION TESTS ===\n");
//
// // Test PolynomialEvaluate with variables
// double xValue = 2.5;
// double coefA = 1.5;
// double coefB = -2.0;
// double coefC = 3.5;
// double coefD = -1.0;
// Console.WriteLine($"Polynomial f({xValue}) = {coefA}x³ + {coefB}x² + {coefC}x + {coefD} = {Test.PolynomialEvaluate(xValue, coefA, coefB, coefC, coefD):F4}");
//
// // Test FormatFullName with variables
// string firstName = "John";
// string middleName = "Robert";
// string lastName = "Smith";
// bool includeMiddleTrue = true;
// bool includeMiddleFalse = false;
// Console.WriteLine($"Full name (with middle): {Test.FormatFullName(firstName, middleName, lastName, includeMiddleTrue)}");
// Console.WriteLine($"Full name (without middle): {Test.FormatFullName(firstName, middleName, lastName, includeMiddleFalse)}");
//
// // Test TriangleArea with variables
// double sideA = 5.0;
// double sideB = 7.0;
// double angleC = 60.0;
// Console.WriteLine($"Triangle area (sides {sideA}, {sideB}, angle {angleC}°) = {Test.TriangleArea(sideA, sideB, angleC):F4}");
//
// // Test WeightedAverage with variables
// double val1 = 85.0;
// double weight1 = 0.3;
// double val2 = 90.0;
// double weight2 = 0.5;
// double val3 = 75.0;
// double weight3 = 0.2;
// Console.WriteLine($"Weighted average of [{val1}×{weight1}, {val2}×{weight2}, {val3}×{weight3}] = {Test.WeightedAverage(val1, weight1, val2, weight2, val3, weight3):F4}");
//
// // Test DaysBetweenDates with variables
// int year1 = 2020;
// int month1 = 1;
// int day1 = 15;
// int year2 = 2025;
// int month2 = 10;
// int day2 = 14;
// Console.WriteLine($"Days between {year1}/{month1}/{day1} and {year2}/{month2}/{day2} = {Test.DaysBetweenDates(year1, month1, day1, year2, month2, day2)}");
//
// // Test DetermineGrade with variables
// double studentScore = 85.5;
// double maxScore = 100.0;
// bool useCurve = true;
// double curveBonus = 5.0;
// Console.WriteLine($"Grade for {studentScore}/{maxScore} (curve: {useCurve}, bonus: {curveBonus}) = {Test.DetermineGrade(studentScore, maxScore, useCurve, curveBonus)}");
//
// double studentScore2 = 85.5;
// bool noCurve = false;
// Console.WriteLine($"Grade for {studentScore2}/{maxScore} (no curve) = {Test.DetermineGrade(studentScore2, maxScore, noCurve, 0.0)}");

// Test ProjectileMaxHeight with variables
double velocity = 50.0;
double angle = 45.0;
double gravity = 9.81;
Console.WriteLine($"Projectile max height (v={velocity} m/s, angle={angle}°, g={gravity} m/s²) = {Test.ProjectileMaxHeight(velocity, angle, gravity):F2} m");
//
// // Test CompoundInterest with variables
// double principal = 1000.0;
// double rate = 0.05;
// int timesCompounded = 12;
// double years = 10.0;
// Console.WriteLine($"Compound interest: ${principal} at {rate * 100}% compounded {timesCompounded}x/year for {years} years = ${Test.CompoundInterest(principal, rate, timesCompounded, years):F2}");
//
// // Test GenerateSlug with variables
// string text = "Hello World Example 2024";
// int maxLength = 20;
// char separator = '-';
// bool toLowerCase = true;
// Console.WriteLine($"Slug from '{text}' (max {maxLength}, sep '{separator}', lower: {toLowerCase}) = '{Test.GenerateSlug(text, maxLength, separator, toLowerCase)}'");
//
// string text2 = "C# Programming Tutorial";
// int maxLength2 = 15;
// char separator2 = '_';
// bool upperCase = false;
// Console.WriteLine($"Slug from '{text2}' (max {maxLength2}, sep '{separator2}', lower: {upperCase}) = '{Test.GenerateSlug(text2, maxLength2, separator2, upperCase)}'");
//
// // Test FilterAndTransform with variables
// int[] numbers = { 1, 5, 10, 15, 20, 25, 30 };
// int minValue = 10;
// int maxValue = 25;
// int multiplier = 2;
// Console.WriteLine($"Filter [{String.Join(", ", numbers)}] range [{minValue}..{maxValue}] × {multiplier} = [{String.Join(", ", Test.FilterAndTransform(numbers, minValue, maxValue, multiplier))}]");
