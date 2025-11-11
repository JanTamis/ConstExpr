using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class MathOperationsTests
	{
		public static void RunTests(int varInt, int varInt2, int varInt3, int varInt4)
		{
			Console.WriteLine("??? MATH OPERATIONS ???\n");

			// Factorial - alleen constanten
			Console.WriteLine($"[CONST] Factorial(5): {MathOperations.Factorial(5)}");
			Console.WriteLine($"[CONST] Factorial(7): {MathOperations.Factorial(7)}");
			// Factorial - mixed
			Console.WriteLine($"[MIXED] Factorial(varInt2): {MathOperations.Factorial(varInt2)}");

			// Fibonacci - alleen constanten
			Console.WriteLine($"[CONST] Fibonacci(10): {MathOperations.Fibonacci(10)}");
			Console.WriteLine($"[CONST] Fibonacci(15): {MathOperations.Fibonacci(15)}");
			// Fibonacci - mixed
			Console.WriteLine($"[MIXED] Fibonacci(varInt): {MathOperations.Fibonacci(varInt)}");

			// IsPrime - alleen constanten
			Console.WriteLine($"[CONST] IsPrime(17): {MathOperations.IsPrime(17)}");
			Console.WriteLine($"[CONST] IsPrime(18): {MathOperations.IsPrime(18)}");
			Console.WriteLine($"[CONST] IsPrime(97): {MathOperations.IsPrime(97)}");
			// IsPrime - mixed
			Console.WriteLine($"[MIXED] IsPrime(varInt3): {MathOperations.IsPrime(varInt3)}");

			// GCD - alleen constanten
			Console.WriteLine($"[CONST] GCD(48, 18): {MathOperations.GCD(48, 18)}");
			Console.WriteLine($"[CONST] GCD(100, 45): {MathOperations.GCD(100, 45)}");
			// GCD - mixed
			Console.WriteLine($"[MIXED] GCD(48, varInt3): {MathOperations.GCD(48, varInt3)}");

			// LCM - alleen constanten
			Console.WriteLine($"[CONST] LCM(12, 18): {MathOperations.LCM(12, 18)}");
			Console.WriteLine($"[CONST] LCM(15, 20): {MathOperations.LCM(15, 20)}");
			// LCM - mixed
			Console.WriteLine($"[MIXED] LCM(varInt4, varInt2): {MathOperations.LCM(varInt4, varInt2)}");

			// Power - alleen constanten
			Console.WriteLine($"[CONST] Power(2, 10): {MathOperations.Power(2, 10)}");
			Console.WriteLine($"[CONST] Power(3, 5): {MathOperations.Power(3, 5)}");
			// Power - mixed
			Console.WriteLine($"[MIXED] Power(2, varInt2): {MathOperations.Power(2, varInt2)}");

			// SumOfDigits - alleen constanten
			Console.WriteLine($"[CONST] SumOfDigits(12345): {MathOperations.SumOfDigits(12345)}");
			Console.WriteLine($"[CONST] SumOfDigits(999): {MathOperations.SumOfDigits(999)}");
			// SumOfDigits - mixed
			Console.WriteLine($"[MIXED] SumOfDigits(varInt): {MathOperations.SumOfDigits(varInt)}");

			// IsPerfectSquare - alleen constanten
			Console.WriteLine($"[CONST] IsPerfectSquare(16): {MathOperations.IsPerfectSquare(16)}");
			Console.WriteLine($"[CONST] IsPerfectSquare(15): {MathOperations.IsPerfectSquare(15)}");
			Console.WriteLine($"[CONST] IsPerfectSquare(144): {MathOperations.IsPerfectSquare(144)}");

			// CountDigits - alleen constanten
			Console.WriteLine($"[CONST] CountDigits(12345): {MathOperations.CountDigits(12345)}");
			Console.WriteLine($"[CONST] CountDigits(999999): {MathOperations.CountDigits(999999)}");

			// ReverseNumber - alleen constanten
			Console.WriteLine($"[CONST] ReverseNumber(12345): {MathOperations.ReverseNumber(12345)}");
			Console.WriteLine($"[CONST] ReverseNumber(-987): {MathOperations.ReverseNumber(-987)}");
			// ReverseNumber - mixed
			Console.WriteLine($"[MIXED] ReverseNumber(varInt3): {MathOperations.ReverseNumber(varInt3)}");

			// SumRange - alleen constanten
			Console.WriteLine($"[CONST] SumRange(1, 100): {MathOperations.SumRange(1, 100)}");
			Console.WriteLine($"[CONST] SumRange(10, 20): {MathOperations.SumRange(10, 20)}");
			// SumRange - mixed
			Console.WriteLine($"[MIXED] SumRange(1, varInt3): {MathOperations.SumRange(1, varInt3)}");

			// BinomialCoefficient - alleen constanten
			Console.WriteLine($"[CONST] BinomialCoefficient(10, 3): {MathOperations.BinomialCoefficient(10, 3)}");
			Console.WriteLine($"[CONST] BinomialCoefficient(20, 5): {MathOperations.BinomialCoefficient(20, 5)}");
			// BinomialCoefficient - mixed
			Console.WriteLine($"[MIXED] BinomialCoefficient(varInt, varInt2): {MathOperations.BinomialCoefficient(varInt, varInt2)}");

			Console.WriteLine();
		}
	}
}
