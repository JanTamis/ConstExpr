using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests
{
	internal class CryptographyTests
	{
		public static void RunTests(byte varByte, int varInt2, string varString)
		{
			Console.WriteLine("═══ CRYPTOGRAPHY OPERATIONS ═══\n");

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
		}
	}
}