using ConstExpr.SourceGenerator.Sample;
using System;
using System.Linq;

Console.WriteLine("╔═══════════════════════════════════════���════════════════════════╗");
Console.WriteLine("║  ConstExpr Test Suite - Alle functies met constanten & vars      ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝\n");

// Variabelen voor mixed tests
var varInt = 10;
var varDouble = 5.5;
var varFloat = 3.14f;
var varByte = (byte)128;
var varString = "TestString";
var varYear = 2024;
var varMonth = 6;
var varDay = 15;

// Extra variabelen voor var-only tests
var varInt2 = 5;
var varInt3 = 20;
var varInt4 = 3;
var varInt5 = 15;
var varInt6 = 48;
var varInt7 = 18;
var varInt8 = 12;
var varDouble2 = 2.5;
var varDouble3 = 10.0;
var varDouble4 = 0.5;
var varDouble5 = 100.0;
var varDouble6 = 4.0;
var varDouble7 = 6.0;
var varDouble8 = 8.0;
var varDouble9 = 0.3;
var varDouble10 = 0.2;
var varDouble11 = 85.0;
var varDouble12 = 90.0;
var varDouble13 = 75.0;

// ════════════════════════════════════════════════���══════════════
// CRYPTOGRAPHY OPERATIONS
// ═══════════════════════════════════════════════════════════════
//Console.WriteLine("═══ CRYPTOGRAPHY OPERATIONS ═══\n");

// CalculateChecksum - alleen constanten
Console.WriteLine($"[CONST] CalculateChecksum(1,2,3,4,5): {CryptographyOperations.CalculateChecksum(1, 2, 3, 4, 5)}");
// CalculateChecksum - mixed
Console.WriteLine($"[MIXED] CalculateChecksum(varByte,255,128,64): {CryptographyOperations.CalculateChecksum(varByte, 255, 128, 64)}");

// CaesarEncrypt - alleen constanten
Console.WriteLine($"[CONST] CaesarEncrypt(\"HELLO\",3): {CryptographyOperations.CaesarEncrypt("HELLO", 3)}");
// CaesarEncrypt - mixed
Console.WriteLine($"[MIXED] CaesarEncrypt(\"HELLO\",varInt2): {CryptographyOperations.CaesarEncrypt("HELLO", varInt2)}");

// CaesarDecrypt - alleen constanten
Console.WriteLine($"[CONST] CaesarDecrypt(\"KHOOR\",3): {CryptographyOperations.CaesarDecrypt("KHOOR", 3)}");
// CaesarDecrypt - mixed
Console.WriteLine($"[MIXED] CaesarDecrypt(\"KHOOR\",varInt2): {CryptographyOperations.CaesarDecrypt("KHOOR", varInt2)}");

// IsValidHex - alleen constanten
Console.WriteLine($"[CONST] IsValidHex(\"1A2F3C\"): {CryptographyOperations.IsValidHex("1A2F3C")}");
Console.WriteLine($"[CONST] IsValidHex(\"XYZ\"): {CryptographyOperations.IsValidHex("XYZ")}");

// BytesToHex - alleen constanten
Console.WriteLine($"[CONST] BytesToHex(255,128,64,32): {CryptographyOperations.BytesToHex(255, 128, 64, 32)}");
// BytesToHex - mixed
Console.WriteLine($"[MIXED] BytesToHex(varByte,255,128): {CryptographyOperations.BytesToHex(varByte, 255, 128)}");

// PolynomialHash - alleen constanten
Console.WriteLine($"[CONST] PolynomialHash(\"hello\"): {CryptographyOperations.PolynomialHash("hello")}");
// PolynomialHash - mixed
Console.WriteLine($"[MIXED] PolynomialHash(varString): {CryptographyOperations.PolynomialHash(varString)}");

Console.WriteLine();

// ════════════════════════════════════════���══════════════════════
// DATA VALIDATION OPERATIONS
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("═══ DATA VALIDATION OPERATIONS ═══\n");

// IsInRange - alleen constanten
Console.WriteLine($"[CONST] IsInRange(5.5, 0, 10): {DataValidationOperations.IsInRange(5.5, 0, 10)}");
Console.WriteLine($"[CONST] IsInRange(15, 0, 10): {DataValidationOperations.IsInRange(15, 0, 10)}");

// IsInRange - mixed
Console.WriteLine($"[MIXED] IsInRange(varDouble, 0, 10): {DataValidationOperations.IsInRange(varDouble, 0, 10)}");

// AllPositive - alleen constanten
Console.WriteLine($"[CONST] AllPositive(1, 2, 3, 4, 5): {DataValidationOperations.AllPositive(1, 2, 3, 4, 5)}");
Console.WriteLine($"[CONST] AllPositive(1, -2, 3): {DataValidationOperations.AllPositive(1, -2, 3)}");

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
