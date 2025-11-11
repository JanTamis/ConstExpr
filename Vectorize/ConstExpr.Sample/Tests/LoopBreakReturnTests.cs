using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class LoopBreakReturnTests
	{
		public static void RunTests(int varInt2, string varString, int varInt)
		{
			Console.WriteLine("═══ LOOP BREAK & RETURN TESTS ═══\n");

			// CountUntilTarget - Tests break in while(true) loop
			Console.WriteLine($"[CONST] CountUntilTarget(5): {DataValidationOperations.CountUntilTarget(5)}");
			Console.WriteLine($"[CONST] CountUntilTarget(10): {DataValidationOperations.CountUntilTarget(10)}");
			Console.WriteLine($"[MIXED] CountUntilTarget(varInt2): {DataValidationOperations.CountUntilTarget(varInt2)}");

			// FindFirstEven - Tests return in for loop
			Console.WriteLine($"[CONST] FindFirstEven(): {DataValidationOperations.FindFirstEven()}");

			// FindCharIndex - Tests break in foreach loop
			Console.WriteLine($"[CONST] FindCharIndex(\"HELLO\", 'L'): {DataValidationOperations.FindCharIndex("HELLO", 'L')}");
			Console.WriteLine($"[CONST] FindCharIndex(\"HELLO\", 'X'): {DataValidationOperations.FindCharIndex("HELLO", 'X')}");
			Console.WriteLine($"[MIXED] FindCharIndex(varString, 'T'): {DataValidationOperations.FindCharIndex(varString, 'T')}");

			// CountInGrid - Tests nested loops with break
			Console.WriteLine($"[CONST] CountInGrid(3, 3, 5): {DataValidationOperations.CountInGrid(3, 3, 5)}");
			Console.WriteLine($"[CONST] CountInGrid(2, 4, 6): {DataValidationOperations.CountInGrid(2, 4, 6)}");

			// SumUntilLimit - Tests while(true) with early return
			Console.WriteLine($"[CONST] SumUntilLimit(10): {DataValidationOperations.SumUntilLimit(10)}");
			Console.WriteLine($"[CONST] SumUntilLimit(20): {DataValidationOperations.SumUntilLimit(20)}");
			Console.WriteLine($"[MIXED] SumUntilLimit(varInt * 2): {DataValidationOperations.SumUntilLimit(varInt * 2)}");

			Console.WriteLine();
		}
	}
}