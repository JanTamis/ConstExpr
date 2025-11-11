using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class ArrayOperationsTests
	{
		public static void RunTests(int varInt, int varInt2, int varInt3)
		{
			Console.WriteLine("??? ARRAY OPERATIONS ???\n");

			// FindMax - alleen constanten
			Console.WriteLine($"[CONST] FindMax(5, 12, 3, 18, 7): {ArrayOperations.FindMax(5, 12, 3, 18, 7)}");
			Console.WriteLine($"[CONST] FindMax(100, 50, 200, 25): {ArrayOperations.FindMax(100, 50, 200, 25)}");
			// FindMax - mixed
			Console.WriteLine($"[MIXED] FindMax(varInt, 15, varInt3, 8): {ArrayOperations.FindMax(varInt, 15, varInt3, 8)}");

			// FindMin - alleen constanten
			Console.WriteLine($"[CONST] FindMin(5, 12, 3, 18, 7): {ArrayOperations.FindMin(5, 12, 3, 18, 7)}");
			Console.WriteLine($"[CONST] FindMin(100, 50, 200, 25): {ArrayOperations.FindMin(100, 50, 200, 25)}");
			// FindMin - mixed
			Console.WriteLine($"[MIXED] FindMin(varInt, 15, varInt3, 8): {ArrayOperations.FindMin(varInt, 15, varInt3, 8)}");

			// Average - alleen constanten
			Console.WriteLine($"[CONST] Average(10, 20, 30, 40, 50): {ArrayOperations.Average(10, 20, 30, 40, 50):F2}");
			Console.WriteLine($"[CONST] Average(5, 15, 25): {ArrayOperations.Average(5, 15, 25):F2}");
			// Average - mixed
			Console.WriteLine($"[MIXED] Average(varInt, varInt2, varInt3): {ArrayOperations.Average(varInt, varInt2, varInt3):F2}");

			// Median - alleen constanten
			Console.WriteLine($"[CONST] Median(3, 1, 4, 1, 5, 9, 2, 6): {ArrayOperations.Median(3, 1, 4, 1, 5, 9, 2, 6):F2}");
			Console.WriteLine($"[CONST] Median(10, 20, 30, 40, 50): {ArrayOperations.Median(10, 20, 30, 40, 50):F2}");

			// IsSorted - alleen constanten
			Console.WriteLine($"[CONST] IsSorted(1, 2, 3, 4, 5): {ArrayOperations.IsSorted(1, 2, 3, 4, 5)}");
			Console.WriteLine($"[CONST] IsSorted(1, 3, 2, 4, 5): {ArrayOperations.IsSorted(1, 3, 2, 4, 5)}");
			Console.WriteLine($"[CONST] IsSorted(5, 10, 15, 20, 25): {ArrayOperations.IsSorted(5, 10, 15, 20, 25)}");

			// RemoveDuplicates - alleen constanten
			Console.WriteLine($"[CONST] RemoveDuplicates(1, 2, 2, 3, 3, 3, 4): [{string.Join(", ", ArrayOperations.RemoveDuplicates(1, 2, 2, 3, 3, 3, 4))}]");
			Console.WriteLine($"[CONST] RemoveDuplicates(5, 5, 5, 5): [{string.Join(", ", ArrayOperations.RemoveDuplicates(5, 5, 5, 5))}]");

			// CountOccurrences - alleen constanten
			Console.WriteLine($"[CONST] CountOccurrences(3, [1, 2, 3, 3, 4, 3, 5]): {ArrayOperations.CountOccurrences(3, 1, 2, 3, 3, 4, 3, 5)}");
			Console.WriteLine($"[CONST] CountOccurrences(7, [1, 2, 3, 4, 5]): {ArrayOperations.CountOccurrences(7, 1, 2, 3, 4, 5)}");
			// CountOccurrences - mixed
			Console.WriteLine($"[MIXED] CountOccurrences(varInt2, [1, 5, 2, 5, 3, 5]): {ArrayOperations.CountOccurrences(varInt2, 1, 5, 2, 5, 3, 5)}");

			// IndexOf - alleen constanten
			Console.WriteLine($"[CONST] IndexOf(42, [10, 20, 42, 30, 40]): {ArrayOperations.IndexOf(42, 10, 20, 42, 30, 40)}");
			Console.WriteLine($"[CONST] IndexOf(99, [10, 20, 30, 40]): {ArrayOperations.IndexOf(99, 10, 20, 30, 40)}");

			// Reverse - alleen constanten
			Console.WriteLine($"[CONST] Reverse(1, 2, 3, 4, 5): [{string.Join(", ", ArrayOperations.Reverse(1, 2, 3, 4, 5))}]");
			Console.WriteLine($"[CONST] Reverse(10, 20, 30): [{string.Join(", ", ArrayOperations.Reverse(10, 20, 30))}]");

			// RotateLeft - alleen constanten
			Console.WriteLine($"[CONST] RotateLeft(2, [1, 2, 3, 4, 5]): [{string.Join(", ", ArrayOperations.RotateLeft(2, 1, 2, 3, 4, 5))}]");
			Console.WriteLine($"[CONST] RotateLeft(3, [10, 20, 30, 40]): [{string.Join(", ", ArrayOperations.RotateLeft(3, 10, 20, 30, 40))}]");
			// RotateLeft - mixed
			Console.WriteLine($"[MIXED] RotateLeft(varInt2, [1, 2, 3, 4, 5, 6]): [{string.Join(", ", ArrayOperations.RotateLeft(varInt2, 1, 2, 3, 4, 5, 6))}]");

			// Sum - alleen constanten
			Console.WriteLine($"[CONST] Sum(1, 2, 3, 4, 5): {ArrayOperations.Sum(1, 2, 3, 4, 5)}");
			Console.WriteLine($"[CONST] Sum(10, 20, 30, 40): {ArrayOperations.Sum(10, 20, 30, 40)}");
			// Sum - mixed
			Console.WriteLine($"[MIXED] Sum(varInt, varInt2, varInt3): {ArrayOperations.Sum(varInt, varInt2, varInt3)}");

			// Product - alleen constanten
			Console.WriteLine($"[CONST] Product(2, 3, 4, 5): {ArrayOperations.Product(2, 3, 4, 5)}");
			Console.WriteLine($"[CONST] Product(10, 10, 10): {ArrayOperations.Product(10, 10, 10)}");

			// SecondLargest - alleen constanten
			Console.WriteLine($"[CONST] SecondLargest(5, 12, 3, 18, 7, 15): {ArrayOperations.SecondLargest(5, 12, 3, 18, 7, 15)}");
			Console.WriteLine($"[CONST] SecondLargest(100, 50, 200, 25, 175): {ArrayOperations.SecondLargest(100, 50, 200, 25, 175)}");

			// Contains - alleen constanten
			Console.WriteLine($"[CONST] Contains(42, [10, 20, 42, 30]): {ArrayOperations.Contains(42, 10, 20, 42, 30)}");
			Console.WriteLine($"[CONST] Contains(99, [10, 20, 30, 40]): {ArrayOperations.Contains(99, 10, 20, 30, 40)}");
			// Contains - mixed
			Console.WriteLine($"[MIXED] Contains(varInt2, [1, 5, 10, 15, 20]): {ArrayOperations.Contains(varInt2, 1, 5, 10, 15, 20)}");

			// Range - alleen constanten
			Console.WriteLine($"[CONST] Range(3, 7, 2, 9, 1, 5): {ArrayOperations.Range(3, 7, 2, 9, 1, 5)}");
			Console.WriteLine($"[CONST] Range(100, 50, 75, 125, 25): {ArrayOperations.Range(100, 50, 75, 125, 25)}");

			Console.WriteLine();
		}
	}
}
