using System;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests;

internal partial class RegexOperationsTests
{
	[GeneratedRegex(@"\d+")]
	private static partial Regex NumberRegex();
		
	public static void RunTests(string varString, int varInt)
	{
		Console.WriteLine("??? REGEX OPERATIONS ???\n");

		// MatchesWildcard - alleen constanten
		Console.WriteLine($"[CONST] MatchesWildcard(\"Hello World\", \"Hello*\"): {RegexOperations.MatchesWildcard("Hello World", "Hello*")}");
		Console.WriteLine($"[CONST] MatchesWildcard(\"Hello World\", \"H?llo*\"): {RegexOperations.MatchesWildcard("Hello World", "H?llo*")}");
		Console.WriteLine($"[CONST] MatchesWildcard(\"Hello\", \"World\"): {RegexOperations.MatchesWildcard("Hello", "World")}");
		// MatchesWildcard - mixed
		Console.WriteLine($"[MIXED] MatchesWildcard(varString, \"Test*\"): {RegexOperations.MatchesWildcard(varString, "Test*")}");

		// CountSubstringMatches - alleen constanten
		Console.WriteLine($"[CONST] CountSubstringMatches(\"abcabcabc\", \"abc\"): {RegexOperations.CountSubstringMatches("abcabcabc", "abc")}");
		Console.WriteLine($"[CONST] CountSubstringMatches(\"Mississippi\", \"ss\"): {RegexOperations.CountSubstringMatches("Mississippi", "ss")}");
		// CountSubstringMatches - mixed
		// Console.WriteLine($"[MIXED] CountSubstringMatches(varString, \"Test\"): {RegexOperations.CountSubstringMatches(varString, "Test")}");

		// IsValidIpAddress - alleen constanten
		Console.WriteLine($"[CONST] IsValidIpAddress(\"192.168.1.1\"): {RegexOperations.IsValidIpAddress("192.168.1.1")}");
		Console.WriteLine($"[CONST] IsValidIpAddress(\"255.255.255.255\"): {RegexOperations.IsValidIpAddress("255.255.255.255")}");
		Console.WriteLine($"[CONST] IsValidIpAddress(\"300.1.1.1\"): {RegexOperations.IsValidIpAddress("300.1.1.1")}");
		// IsValidIpAddress - mixed
		Console.WriteLine($"[MIXED] IsValidIpAddress(varString): {RegexOperations.IsValidIpAddress(varString)}");

		// IsHexColor - alleen constanten
		Console.WriteLine($"[CONST] IsHexColor(\"#FF5733\"): {RegexOperations.IsHexColor("#FF5733")}");
		Console.WriteLine($"[CONST] IsHexColor(\"#f3a\"): {RegexOperations.IsHexColor("#f3a")}");
		Console.WriteLine($"[CONST] IsHexColor(\"#GGGGGG\"): {RegexOperations.IsHexColor("#GGGGGG")}");
		// IsHexColor - mixed
		Console.WriteLine($"[MIXED] IsHexColor(varString): {RegexOperations.IsHexColor(varString)}");

		// IsValidIdentifier - alleen constanten
		Console.WriteLine($"[CONST] IsValidIdentifier(\"myVariable\"): {RegexOperations.IsValidIdentifier("myVariable")}");
		Console.WriteLine($"[CONST] IsValidIdentifier(\"_private\"): {RegexOperations.IsValidIdentifier("_private")}");
		Console.WriteLine($"[CONST] IsValidIdentifier(\"123abc\"): {RegexOperations.IsValidIdentifier("123abc")}");
		// IsValidIdentifier - mixed
		Console.WriteLine($"[MIXED] IsValidIdentifier(varString): {RegexOperations.IsValidIdentifier(varString)}");

		// CountDigitGroups - alleen constanten
		Console.WriteLine($"[CONST] CountDigitGroups(\"abc123def456\"): {RegexOperations.CountDigitGroups("abc123def456")}");
		Console.WriteLine($"[CONST] CountDigitGroups(\"hello\"): {RegexOperations.CountDigitGroups("hello")}");
		Console.WriteLine($"[CONST] CountDigitGroups(\"1 plus 2 equals 3\"): {RegexOperations.CountDigitGroups("1 plus 2 equals 3")}");
		// CountDigitGroups - mixed
		// Console.WriteLine($"[MIXED] CountDigitGroups(varString): {RegexOperations.CountDigitGroups(varString)}");

		// ExtractDigits - alleen constanten
		Console.WriteLine($"[CONST] ExtractDigits(\"abc123def456\"): {RegexOperations.ExtractDigits("abc123def456")}");
		Console.WriteLine($"[CONST] ExtractDigits(\"Phone: +31 6-12345678\"): {RegexOperations.ExtractDigits("Phone: +31 6-12345678")}");
		// ExtractDigits - mixed
		Console.WriteLine($"[MIXED] ExtractDigits(varString): {RegexOperations.ExtractDigits(varString)}");

		// HasRepeatingBlock - alleen constanten
		Console.WriteLine($"[CONST] HasRepeatingBlock(\"abcabc\", 3): {RegexOperations.HasRepeatingBlock("abcabc", 3)}");
		Console.WriteLine($"[CONST] HasRepeatingBlock(\"abcdef\", 3): {RegexOperations.HasRepeatingBlock("abcdef", 3)}");
		Console.WriteLine($"[CONST] HasRepeatingBlock(\"xyzxyz\", 2): {RegexOperations.HasRepeatingBlock("xyzxyz", 2)}");
		// HasRepeatingBlock - mixed
		Console.WriteLine($"[MIXED] HasRepeatingBlock(varString, varInt): {RegexOperations.HasRepeatingBlock(varString, varInt)}");

		// MatchesSimplePattern - alleen constanten
		Console.WriteLine($"[CONST] MatchesSimplePattern(\"a1b\", \"\\\\w\\\\d\\\\w\"): {RegexOperations.MatchesSimplePattern("a1b", @"\w\d\w")}");
		Console.WriteLine($"[CONST] MatchesSimplePattern(\"123\", \"\\\\d\\\\d\\\\d\"): {RegexOperations.MatchesSimplePattern("123", @"\d\d\d")}");
		Console.WriteLine($"[CONST] MatchesSimplePattern(\"abc\", \"\\\\d\\\\d\\\\d\"): {RegexOperations.MatchesSimplePattern("abc", @"\d\d\d")}");
		// MatchesSimplePattern - mixed
		Console.WriteLine($"[MIXED] MatchesSimplePattern(varString, \"\\\\w*\"): {RegexOperations.MatchesSimplePattern(varString, @"\w\w\w\w\w\w\w\w\w\w")}");

		// IndexOfPattern - alleen constanten
		Console.WriteLine($"[CONST] IndexOfPattern(\"Hello World\", \"World\"): {RegexOperations.IndexOfPattern("Hello World", "World")}");
		Console.WriteLine($"[CONST] IndexOfPattern(\"abcabc\", \"bc\"): {RegexOperations.IndexOfPattern("abcabc", "bc")}");
		Console.WriteLine($"[CONST] IndexOfPattern(\"Hello\", \"xyz\"): {RegexOperations.IndexOfPattern("Hello", "xyz")}");
		// IndexOfPattern - mixed
		Console.WriteLine($"[MIXED] IndexOfPattern(varString, \"String\"): {RegexOperations.IndexOfPattern(varString, "String")}");

		// ReplaceAll - alleen constanten
		Console.WriteLine($"[CONST] ReplaceAll(\"Hello World\", \"World\", \"C#\"): {RegexOperations.ReplaceAll("Hello World", "World", "C#")}");
		Console.WriteLine($"[CONST] ReplaceAll(\"aabbcc\", \"bb\", \"XX\"): {RegexOperations.ReplaceAll("aabbcc", "bb", "XX")}");
		Console.WriteLine($"[CONST] ReplaceAll(\"abcabcabc\", \"abc\", \"-\"): {RegexOperations.ReplaceAll("abcabcabc", "abc", "-")}");
		// ReplaceAll - mixed
		Console.WriteLine($"[MIXED] ReplaceAll(varString, \"Test\", \"Demo\"): {RegexOperations.ReplaceAll(varString, "Test", "Demo")}");

		// StartsWith - alleen constanten
		Console.WriteLine($"[CONST] StartsWith(\"Hello World\", \"Hello\"): {RegexOperations.StartsWith("Hello World", "Hello")}");
		Console.WriteLine($"[CONST] StartsWith(\"Hello World\", \"World\"): {RegexOperations.StartsWith("Hello World", "World")}");
		// StartsWith - mixed
		Console.WriteLine($"[MIXED] StartsWith(varString, \"Test\"): {RegexOperations.StartsWith(varString, "Test")}");

		// EndsWith - alleen constanten
		Console.WriteLine($"[CONST] EndsWith(\"Hello World\", \"World\"): {RegexOperations.EndsWith("Hello World", "World")}");
		Console.WriteLine($"[CONST] EndsWith(\"Hello World\", \"Hello\"): {RegexOperations.EndsWith("Hello World", "Hello")}");
		// EndsWith - mixed
		Console.WriteLine($"[MIXED] EndsWith(varString, \"String\"): {RegexOperations.EndsWith(varString, "String")}");

		// CountSplit - alleen constanten
		Console.WriteLine($"[CONST] CountSplit(\"a,b,c,d\", ','): {RegexOperations.CountSplit("a,b,c,d", ',')}");
		Console.WriteLine($"[CONST] CountSplit(\"Hello World\", ' '): {RegexOperations.CountSplit("Hello World", ' ')}");
		Console.WriteLine($"[CONST] CountSplit(\"one/two/three/four\", '/'): {RegexOperations.CountSplit("one/two/three/four", '/')}");
		// CountSplit - mixed
		Console.WriteLine($"[MIXED] CountSplit(varString, 'e'): {RegexOperations.CountSplit(varString, 'e')}");

		Console.WriteLine();
	}
}