using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class StringOperationsTests
	{
		public static void RunTests(string varString)
		{
			Console.WriteLine("??? STRING OPERATIONS ???\n");

			// Reverse - alleen constanten
			Console.WriteLine($"[CONST] Reverse(\"Hello\"): {StringOperations.Reverse("Hello")}");
			Console.WriteLine($"[CONST] Reverse(\"World\"): {StringOperations.Reverse("World")}");
			// Reverse - mixed
			Console.WriteLine($"[MIXED] Reverse(varString): {StringOperations.Reverse(varString)}");

			// IsPalindrome - alleen constanten
			Console.WriteLine($"[CONST] IsPalindrome(\"racecar\"): {StringOperations.IsPalindrome("racecar")}");
			Console.WriteLine($"[CONST] IsPalindrome(\"hello\"): {StringOperations.IsPalindrome("hello")}");
			Console.WriteLine($"[CONST] IsPalindrome(\"A man a plan a canal Panama\"): {StringOperations.IsPalindrome("A man a plan a canal Panama")}");

			// IsPalindrome - mixed
			Console.WriteLine($"[MIXED] IsPalindrome(varString): {StringOperations.IsPalindrome(varString)}");

			// CountVowels - alleen constanten
			Console.WriteLine($"[CONST] CountVowels(\"Hello World\"): {StringOperations.CountVowels("Hello World")}");
			Console.WriteLine($"[CONST] CountVowels(\"Programming\"): {StringOperations.CountVowels("Programming")}");
			// CountVowels - mixed
			Console.WriteLine($"[MIXED] CountVowels(varString): {StringOperations.CountVowels(varString)}");

			// CountConsonants - alleen constanten
			Console.WriteLine($"[CONST] CountConsonants(\"Hello World\"): {StringOperations.CountConsonants("Hello World")}");
			Console.WriteLine($"[CONST] CountConsonants(\"Programming\"): {StringOperations.CountConsonants("Programming")}");

			// CountConsonants - mixed
			Console.WriteLine($"[MIXED] CountConsonants(varString): {StringOperations.CountConsonants(varString)}");

			// ToTitleCase - alleen constanten
			Console.WriteLine($"[CONST] ToTitleCase(\"hello world\"): {StringOperations.ToTitleCase("hello world")}");
			Console.WriteLine($"[CONST] ToTitleCase(\"the quick brown fox\"): {StringOperations.ToTitleCase("the quick brown fox")}");

			// ToTitleCase - mixed
			Console.WriteLine($"[MIXED] ToTitleCase(varString): {StringOperations.ToTitleCase(varString)}");

			// CountChar - alleen constanten
			Console.WriteLine($"[CONST] CountChar(\"Mississippi\", 's'): {StringOperations.CountChar("Mississippi", 's')}");
			Console.WriteLine($"[CONST] CountChar(\"Hello\", 'l'): {StringOperations.CountChar("Hello", 'l')}");
			// CountChar - mixed
			Console.WriteLine($"[MIXED] CountChar(varString, 'T'): {StringOperations.CountChar(varString, 'T')}");

			// RemoveWhitespace - alleen constanten
			Console.WriteLine($"[CONST] RemoveWhitespace(\"Hello World\"): {StringOperations.RemoveWhitespace("Hello World")}");
			Console.WriteLine($"[CONST] RemoveWhitespace(\"  Test  String  \"): {StringOperations.RemoveWhitespace("  Test  String  ")}");

			// RemoveWhitespace - mixed
			Console.WriteLine($"[MIXED] RemoveWhitespace(varString): {StringOperations.RemoveWhitespace(varString)}");

			// IsNumeric - alleen constanten
			Console.WriteLine($"[CONST] IsNumeric(\"12345\"): {StringOperations.IsNumeric("12345")}");
			Console.WriteLine($"[CONST] IsNumeric(\"123a45\"): {StringOperations.IsNumeric("123a45")}");
			Console.WriteLine($"[CONST] IsNumeric(\"999999\"): {StringOperations.IsNumeric("999999")}");

			// IsNumeric - mixed
			Console.WriteLine($"[MIXED] IsNumeric(varString): {StringOperations.IsNumeric(varString)}");

			// CountWords - alleen constanten
			Console.WriteLine($"[CONST] CountWords(\"Hello World\"): {StringOperations.CountWords("Hello World")}");
			Console.WriteLine($"[CONST] CountWords(\"The quick brown fox jumps\"): {StringOperations.CountWords("The quick brown fox jumps")}");

			// CountWords - mixed
			Console.WriteLine($"[MIXED] CountWords(varString): {StringOperations.CountWords(varString)}");

			// Repeat - alleen constanten
			Console.WriteLine($"[CONST] Repeat(\"Ha\", 3): {StringOperations.Repeat("Ha", 3)}");
			Console.WriteLine($"[CONST] Repeat(\"Echo \", 4): {StringOperations.Repeat("Echo ", 4)}");

			// Repeat - mixed
			Console.WriteLine($"[MIXED] Repeat(varString, 3): {StringOperations.Repeat(varString, 3)}");

			// ToAlternatingCase - alleen constanten
			Console.WriteLine($"[CONST] ToAlternatingCase(\"Hello World\"): {StringOperations.ToAlternatingCase("Hello World")}");
			Console.WriteLine($"[CONST] ToAlternatingCase(\"ABC123xyz\"): {StringOperations.ToAlternatingCase("ABC123xyz")}");

			// ToAlternatingCase - mixed
			Console.WriteLine($"[MIXED] ToAlternatingCase(varString): {StringOperations.ToAlternatingCase(varString)}");

			// AreAnagrams - alleen constanten
			Console.WriteLine($"[CONST] AreAnagrams(\"listen\", \"silent\"): {StringOperations.AreAnagrams("listen", "silent")}");
			Console.WriteLine($"[CONST] AreAnagrams(\"hello\", \"world\"): {StringOperations.AreAnagrams("hello", "world")}");
			Console.WriteLine($"[CONST] AreAnagrams(\"Astronomer\", \"Moon starer\"): {StringOperations.AreAnagrams("Astronomer", "Moon starer")}");

			// AreAnagrams - mixed
			Console.WriteLine($"[MIXED] AreAnagrams(varString, \"test\"): {StringOperations.AreAnagrams(varString, "test")}");
			Console.WriteLine($"[MIXED] AreAnagrams(varString, varString): {StringOperations.AreAnagrams(varString, varString)}");

			Console.WriteLine();
		}
	}
}