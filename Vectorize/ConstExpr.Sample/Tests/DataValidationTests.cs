using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class DataValidationTests
	{
		public static void RunTests(double varDouble, string varString, int varInt)
		{
			Console.WriteLine("═══ DATA VALIDATION OPERATIONS ═══\n");

			// IsInRange - alleen constanten
			Console.WriteLine($"[CONST] IsInRange(5.5, 0, 10): {DataValidationOperations.IsInRange(5.5, 0, 10)}");
			Console.WriteLine($"[CONST] IsInRange(15, 0, 10): {DataValidationOperations.IsInRange(15, 0, 10)}");

			// IsInRange - mixed
			Console.WriteLine($"[MIXED] IsInRange(varDouble, 0, 10): {DataValidationOperations.IsInRange(varDouble, 0, 10)}");

			// AllPositive - alleen constanten
			Console.WriteLine($"[CONST] AllPositive(1, 2, 3, 4, 5): {DataValidationOperations.AllPositive(1, 2, 3, 4, 5)}");
			// Console.WriteLine($"[CONST] AllPositive(1, -2, 3): {DataValidationOperations.AllPositive(1, -2, 3)}");

			// AllPositive - mixed
			Console.WriteLine($"[MIXED] AllPositive(varDouble, 2, 3): {DataValidationOperations.AllPositive(varDouble, 2, 3)}");

			// IsAlphanumeric - alleen constanten
			Console.WriteLine($"[CONST] IsAlphanumeric(\"Hello123\"): {DataValidationOperations.IsAlphanumeric("Hello123")}");
			Console.WriteLine($"[CONST] IsAlphanumeric(\"Hello@123\"): {DataValidationOperations.IsAlphanumeric("Hello@123")}");

			// IsAlphanumeric - mixed
			Console.WriteLine($"[MIXED] IsAlphanumeric(varString): {DataValidationOperations.IsAlphanumeric(varString)}");

			// IsValidEmail - alleen constanten
			Console.WriteLine($"[CONST] IsValidEmail(\"test@example.com\"): {DataValidationOperations.IsValidEmail("test@example.com")}");
			Console.WriteLine($"[CONST] IsValidEmail(\"invalid.email\"): {DataValidationOperations.IsValidEmail("invalid.email")}");
			Console.WriteLine($"[CONST] IsValidEmail(\"user@domain.co.uk\"): {DataValidationOperations.IsValidEmail("user@domain.co.uk")}");

			// IsValidPhoneNumber - alleen constanten
			Console.WriteLine($"[CONST] IsValidPhoneNumber(\"06-12345678\"): {DataValidationOperations.IsValidPhoneNumber("06-12345678")}");
			Console.WriteLine($"[CONST] IsValidPhoneNumber(\"+31 6 1234 5678\"): {DataValidationOperations.IsValidPhoneNumber("+31 6 1234 5678")}");
			Console.WriteLine($"[CONST] IsValidPhoneNumber(\"123\"): {DataValidationOperations.IsValidPhoneNumber("123")}");

			// CalculateDataQuality - alleen constanten
			Console.WriteLine($"[CONST] CalculateDataQuality(1, 2, 3, 4, 5): {DataValidationOperations.CalculateDataQuality(1, 2, 3, 4, 5):F2}");

			// CalculateDataQuality - mixed
			Console.WriteLine($"[MIXED] CalculateDataQuality(varDouble, 2, 3, 4): {DataValidationOperations.CalculateDataQuality(varDouble, 2, 3, 4):F2}");

			// HasBalancedParentheses - alleen constanten
			Console.WriteLine($"[CONST] HasBalancedParentheses(\"((()))\"): {DataValidationOperations.HasBalancedParentheses("((()))")}");
			Console.WriteLine($"[CONST] HasBalancedParentheses(\"(()\"): {DataValidationOperations.HasBalancedParentheses("(())")}");
			Console.WriteLine($"[CONST] HasBalancedParentheses(\"()()()\"): {DataValidationOperations.HasBalancedParentheses("()()()")}");

			// IsValidCreditCard - alleen constanten (test met geldige Luhn-nummers)
			Console.WriteLine($"[CONST] IsValidCreditCard(\"4532015112830366\"): {DataValidationOperations.IsValidCreditCard("4532015112830366")}");
			Console.WriteLine($"[CONST] IsValidCreditCard(\"1234567890123456\"): {DataValidationOperations.IsValidCreditCard("1234567890123456")}");
			Console.WriteLine($"[CONST] IsValidCreditCard(\"12345\"): {DataValidationOperations.IsValidCreditCard("12345")}");

			Console.WriteLine();
		}
	}
}