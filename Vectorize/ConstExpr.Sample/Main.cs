using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ConstExpr.SourceGenerator.Sample.Tests;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ConstExpr Test Suite - Alle functies met constanten & vars       ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝\n");

// Variabelen voor mixed tests
var varInt = 10;
var varDouble = 5.5;
var varFloat = 3.14f;
var varByte = (byte) 128;
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

// Run test categories
CryptographyTests.RunTests(varByte, varInt2, varString);
DataValidationTests.RunTests(varDouble, varString, varInt);
LoopBreakReturnTests.RunTests(varInt2, varString, varInt);
MathOperationsTests.RunTests(varInt, varInt2, varInt3, varInt4);
StringOperationsTests.RunTests(varString);
ArrayOperationsTests.RunTests(varInt, varInt2, varInt3, new[] { varInt, varInt2, varInt3, varInt4, varInt5 });
RegexOperationsTests.RunTests(varString, varInt);

static bool Any_yCLWdA(ReadOnlySpan<double> data)
{
	if (Vector.IsHardwareAccelerated && data.Length >= Vector<double>.Count)
	{
		var vectors = MemoryMarshal.Cast<double, Vector<double>>(data);
		var acc0 = Vector<double>.AllBitsSet;
		var acc1 = Vector<double>.AllBitsSet;
		var acc2 = Vector<double>.AllBitsSet;
		var acc3 = Vector<double>.AllBitsSet;
		var i = 0;

		for (; i <= vectors.Length - 4; i += 4)
		{
			acc0 &= Vector.GreaterThan<double>(vectors[i], Vector<double>.Zero);
			acc1 &= Vector.GreaterThan<double>(vectors[i + 1], Vector<double>.Zero);
			acc2 &= Vector.GreaterThan<double>(vectors[i + 2], Vector<double>.Zero);
			acc3 &= Vector.GreaterThan<double>(vectors[i + 3], Vector<double>.Zero);
		}

		acc0 &= acc1 & acc2 & acc3;

		if (Vector.NoneWhereAllBitsSet(acc0))
			return false;

		for (; i < vectors.Length; i++)
		{
			if (Vector.NoneWhereAllBitsSet(Vector.GreaterThan(vectors[i], Vector<double>.Zero)))
				return false;
		}

		var tail = data.Length & Vector<double>.Count - 1;

		for (var t = data.Length - tail; t < data.Length; t++)
		{
			if (data[t] <= 0)
				return false;
		}

		return true;
	}

	for (var i = 0; i < data.Length; i++)
	{
		if (data[i] <= 0)
			return false;
	}

	return true;
}