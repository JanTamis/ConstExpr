using ConstExpr.SourceGenerator.Sample;
using ConstExpr.SourceGenerator.Sample.Operations;
using ConstExpr.SourceGenerator.Sample.Tests;
using System;
using System.Linq;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ConstExpr Test Suite - Alle functies met constanten & vars       ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝\n");

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

// Run test categories
//CryptographyTests.RunTests(varByte, varInt2, varString);
//DataValidationTests.RunTests(varDouble, varString, varInt);
//LoopBreakReturnTests.RunTests(varInt2, varString, varInt);
//MathOperationsTests.RunTests(varInt, varInt2, varInt3, varInt4);
//StringOperationsTests.RunTests(varString);
//ArrayOperationsTests.RunTests(varInt, varInt2, varInt3);