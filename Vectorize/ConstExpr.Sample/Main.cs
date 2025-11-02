using ConstExpr.SourceGenerator.Sample;
using System;
using System.Linq;

Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ConstExpr Test Suite - Alle functies met constanten & vars    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

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

// // ═══════════════════════════════════════════════════════════════
// // MATHEMATICAL OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ MATHEMATICAL OPERATIONS ═══\n");
//
// // Average - alleen constanten
// Console.WriteLine($"[CONST] Average(1,2,3,4,5): {MathematicalOperations.Average(1, 2, 3, 4, 5)}");
// // Average - mixed
// Console.WriteLine($"[MIXED] Average(varDouble,2,3): {MathematicalOperations.Average(varDouble, 2, 3)}");
// // Average - alleen variabelen
// Console.WriteLine($"[VARS ] Average(varInt,varInt2,varInt4): {MathematicalOperations.Average(varInt, varInt2, varInt4)}");
//
// // StdDev - alleen constanten
// Console.WriteLine($"[CONST] StdDev(2,4,6,8): {MathematicalOperations.StdDev(2, 4, 6, 8):F3}");
// // StdDev - mixed
// Console.WriteLine($"[MIXED] StdDev(varDouble,4,6): {MathematicalOperations.StdDev(varDouble, 4, 6):F3}");
// // StdDev - alleen variabelen
// Console.WriteLine($"[VARS ] StdDev(varDouble2,varDouble6,varDouble7,varDouble8): {MathematicalOperations.StdDev(varDouble2, varDouble6, varDouble7, varDouble8):F3}");
//
// // Median - alleen constanten
// Console.WriteLine($"[CONST] Median(1,3,2,5,4): {MathematicalOperations.Median(1, 3, 2, 5, 4)}");
// // Median - mixed
// Console.WriteLine($"[MIXED] Median(varDouble,3,2,5): {MathematicalOperations.Median(varDouble, 3, 2, 5)}");
// // Median - alleen variabelen
// Console.WriteLine($"[VARS ] Median(varInt,varInt4,varInt2,varInt5): {MathematicalOperations.Median(varInt, varInt4, varInt2, varInt5)}");
//
// // IsPrime - alleen constanten
// Console.WriteLine($"[CONST] IsPrime(17): {MathematicalOperations.IsPrime(17)}");
// // IsPrime - mixed
// Console.WriteLine($"[MIXED] IsPrime(varInt): {MathematicalOperations.IsPrime(varInt)}");
// // IsPrime - alleen variabelen
// Console.WriteLine($"[VARS ] IsPrime(varInt5): {MathematicalOperations.IsPrime(varInt5)}");
//
// // PrimesUpTo - alleen constanten
// Console.WriteLine($"[CONST] PrimesUpTo(30): {String.Join(", ", MathematicalOperations.PrimesUpTo(30))}");
// // PrimesUpTo - mixed
// Console.WriteLine($"[MIXED] PrimesUpTo(varInt*2): {String.Join(", ", MathematicalOperations.PrimesUpTo(varInt * 2))}");
// // PrimesUpTo - alleen variabelen
// Console.WriteLine($"[VARS ] PrimesUpTo(varInt3): {String.Join(", ", MathematicalOperations.PrimesUpTo(varInt3))}");
//
// // FibonacciSequence - alleen constanten
// Console.WriteLine($"[CONST] Fibonacci(10): {String.Join(", ", MathematicalOperations.FibonacciSequence(10))}");
// // FibonacciSequence - mixed
// Console.WriteLine($"[MIXED] Fibonacci(varInt): {String.Join(", ", MathematicalOperations.FibonacciSequence(varInt))}");
// // FibonacciSequence - alleen variabelen
// Console.WriteLine($"[VARS ] Fibonacci(varInt8): {String.Join(", ", MathematicalOperations.FibonacciSequence(varInt8))}");
//
// // Clamp - alleen constanten
// Console.WriteLine($"[CONST] Clamp(15,0,10): {MathematicalOperations.Clamp(15, 0, 10)}");
// // Clamp - mixed
// Console.WriteLine($"[MIXED] Clamp(varInt,0,20): {MathematicalOperations.Clamp(varInt, 0, 20)}");
// // Clamp - alleen variabelen
// Console.WriteLine($"[VARS ] Clamp(varInt5,varInt2,varInt3): {MathematicalOperations.Clamp(varInt5, varInt2, varInt3)}");
//
// // Map - alleen constanten
// Console.WriteLine($"[CONST] Map(5,0,10,0,100): {MathematicalOperations.Map(5, 0, 10, 0, 100)}");
// // Map - mixed
// Console.WriteLine($"[MIXED] Map(varDouble,0,10,0,100): {MathematicalOperations.Map(varDouble, 0, 10, 0, 100):F2}");
// // Map - alleen variabelen
// Console.WriteLine($"[VARS ] Map(varInt2,varInt,varInt3,varDouble5,varDouble3): {MathematicalOperations.Map(varInt2, varInt, varInt3, varDouble5, varDouble3):F2}");
//
// // Lerp - alleen constanten
// Console.WriteLine($"[CONST] Lerp(0,10,0.5): {MathematicalOperations.Lerp(0, 10, 0.5)}");
// // Lerp - mixed
// Console.WriteLine($"[MIXED] Lerp(0,varDouble,0.5): {MathematicalOperations.Lerp(0, varDouble, 0.5):F2}");
// // Lerp - alleen variabelen
// Console.WriteLine($"[VARS ] Lerp(varInt2,varInt,varDouble4): {MathematicalOperations.Lerp(varInt2, varInt, varDouble4):F2}");
//
// // InverseLerp - alleen constanten
// Console.WriteLine($"[CONST] InverseLerp(0,10,5): {MathematicalOperations.InverseLerp(0, 10, 5)}");
// // InverseLerp - mixed
// Console.WriteLine($"[MIXED] InverseLerp(0,varInt,5): {MathematicalOperations.InverseLerp(0, varInt, 5):F2}");
// // InverseLerp - alleen variabelen
// Console.WriteLine($"[VARS ] InverseLerp(varInt2,varInt3,varInt5): {MathematicalOperations.InverseLerp(varInt2, varInt3, varInt5):F2}");
//
// // GreatestCommonDivisor - alleen constanten
// Console.WriteLine($"[CONST] GCD(48,18): {MathematicalOperations.GreatestCommonDivisor(48, 18)}");
// // GreatestCommonDivisor - mixed
// Console.WriteLine($"[MIXED] GCD(varInt,15): {MathematicalOperations.GreatestCommonDivisor(varInt, 15)}");
// // GreatestCommonDivisor - alleen variabelen
// Console.WriteLine($"[VARS ] GCD(varInt6,varInt7): {MathematicalOperations.GreatestCommonDivisor(varInt6, varInt7)}");
//
// // LeastCommonMultiple - alleen constanten
// Console.WriteLine($"[CONST] LCM(12,18): {MathematicalOperations.LeastCommonMultiple(12, 18)}");
// // LeastCommonMultiple - mixed
// Console.WriteLine($"[MIXED] LCM(varInt,15): {MathematicalOperations.LeastCommonMultiple(varInt, 15)}");
// // LeastCommonMultiple - alleen variabelen
// Console.WriteLine($"[VARS ] LCM(varInt8,varInt7): {MathematicalOperations.LeastCommonMultiple(varInt8, varInt7)}");
//
// // Factorial - alleen constanten
// Console.WriteLine($"[CONST] Factorial(6): {MathematicalOperations.Factorial(6)}");
// // Factorial - mixed
// Console.WriteLine($"[MIXED] Factorial(varInt/2): {MathematicalOperations.Factorial(varInt / 2)}");
// // Factorial - alleen variabelen
// Console.WriteLine($"[VARS ] Factorial(varInt2): {MathematicalOperations.Factorial(varInt2)}");
//
// // Combination - alleen constanten
// Console.WriteLine($"[CONST] Combination(10,3): {MathematicalOperations.Combination(10, 3)}");
// // Combination - mixed
// Console.WriteLine($"[MIXED] Combination(varInt,3): {MathematicalOperations.Combination(varInt, 3)}");
// // Combination - alleen variabelen
// Console.WriteLine($"[VARS ] Combination(varInt,varInt4): {MathematicalOperations.Combination(varInt, varInt4)}");
//
// // Power - alleen constanten
// Console.WriteLine($"[CONST] Power(2,8): {MathematicalOperations.Power(2, 8)}");
// // Power - mixed
// Console.WriteLine($"[MIXED] Power(varDouble,3): {MathematicalOperations.Power(varDouble, 3):F2}");
// // Power - alleen variabelen
// Console.WriteLine($"[VARS ] Power(varDouble2,varInt4): {MathematicalOperations.Power(varDouble2, varInt4):F2}");
//
// // IsPerfectSquare - alleen constanten
// Console.WriteLine($"[CONST] IsPerfectSquare(64): {MathematicalOperations.IsPerfectSquare(64)}");
// // IsPerfectSquare - mixed
// Console.WriteLine($"[MIXED] IsPerfectSquare(varInt): {MathematicalOperations.IsPerfectSquare(varInt)}");
// // IsPerfectSquare - alleen variabelen
// Console.WriteLine($"[VARS ] IsPerfectSquare(varInt6): {MathematicalOperations.IsPerfectSquare(varInt6)}");
//
// // RoundToDecimalPlaces - alleen constanten
// Console.WriteLine($"[CONST] Round(3.14159,2): {MathematicalOperations.RoundToDecimalPlaces(3.14159, 2)}");
// // RoundToDecimalPlaces - mixed
// Console.WriteLine($"[MIXED] Round(varDouble,2): {MathematicalOperations.RoundToDecimalPlaces(varDouble, 2)}");
// // RoundToDecimalPlaces - alleen variabelen
// Console.WriteLine($"[VARS ] Round(varFloat,varInt4): {MathematicalOperations.RoundToDecimalPlaces(varFloat, varInt4)}");
//
// // IsPrime - alleen constanten
// Console.WriteLine($"[CONST] IsPrime(17): {MathematicalOperations.IsPrime(17)}");
// // IsPrime - mixed
// Console.WriteLine($"[MIXED] IsPrime(varInt): {MathematicalOperations.IsPrime(varInt)}");
// // IsPrime - alleen variabelen
// Console.WriteLine($"[VARS ] IsPrime(varInt5): {MathematicalOperations.IsPrime(varInt5)}");
//
// // Clamp - alleen constanten
// Console.WriteLine($"[CONST] Clamp(15,0,10): {MathematicalOperations.Clamp(15, 0, 10)}");
// // Clamp - mixed
// Console.WriteLine($"[MIXED] Clamp(varInt,0,20): {MathematicalOperations.Clamp(varInt, 0, 20)}");
// // Clamp - alleen variabelen
// Console.WriteLine($"[VARS ] Clamp(varInt5,varInt2,varInt): {MathematicalOperations.Clamp(varInt5, varInt2, varInt)}");
//
// // Map - alleen constanten
// Console.WriteLine($"[CONST] Map(5,0,10,0,100): {MathematicalOperations.Map(5, 0, 10, 0, 100):F2}");
// // Map - mixed
// Console.WriteLine($"[MIXED] Map(varDouble,0,10,0,100): {MathematicalOperations.Map(varDouble, 0, 10, 0, 100):F2}");
// // Map - alleen variabelen
// Console.WriteLine($"[VARS ] Map(varDouble2,varInt2,varInt,varInt2,varInt3): {MathematicalOperations.Map(varDouble2, varInt2, varInt, varInt2, varInt3):F2}");
//
// // Power (mathematical) - alleen constanten
// Console.WriteLine($"[CONST] Power(2,8): {MathematicalOperations.Power(2, 8):F2}");
// // Power - mixed
// Console.WriteLine($"[MIXED] Power(varDouble,varInt4): {MathematicalOperations.Power(varDouble, varInt4):F2}");
// // Power - alleen variabelen
// Console.WriteLine($"[VARS ] Power(varDouble2,varInt2): {MathematicalOperations.Power(varDouble2, varInt2):F2}");
//
// Console.WriteLine();
// Console.WriteLine($"[CONST] Polynomial(2,1,-2,3,-1): {MathematicalOperations.PolynomialEvaluate(2, 1, -2, 3, -1):F2}");
// // PolynomialEvaluate - mixed
// Console.WriteLine($"[MIXED] Polynomial(varDouble,1,-2,3,-1): {MathematicalOperations.PolynomialEvaluate(varDouble, 1, -2, 3, -1):F2}");
// // PolynomialEvaluate - alleen variabelen
// Console.WriteLine($"[VARS ] Polynomial(varDouble2,varInt2,varInt4,varInt,varInt5): {MathematicalOperations.PolynomialEvaluate(varDouble2, varInt2, varInt4, varInt, varInt5):F2}");
//
// // WeightedAverage - alleen constanten
// Console.WriteLine($"[CONST] WeightedAvg(85,0.3,90,0.5,75,0.2): {MathematicalOperations.WeightedAverage(85, 0.3, 90, 0.5, 75, 0.2):F2}");
// // WeightedAverage - mixed
// Console.WriteLine($"[MIXED] WeightedAvg(varDouble*10,0.3,90,0.5,75,0.2): {MathematicalOperations.WeightedAverage(varDouble * 10, 0.3, 90, 0.5, 75, 0.2):F2}");
// // WeightedAverage - alleen variabelen
// Console.WriteLine($"[VARS ] WeightedAvg(varDouble11,varDouble9,varDouble12,varDouble4,varDouble13,varDouble10): {MathematicalOperations.WeightedAverage(varDouble11, varDouble9, varDouble12, varDouble4, varDouble13, varDouble10):F2}");

// // ═══════════════════════════════════════════════════════════════
// // DATE TIME OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ DATE TIME OPERATIONS ═══\n");
//
// // DaysBetweenDates - alleen constanten
// Console.WriteLine($"[CONST] DaysBetween(2024,1,1,2024,12,31): {DateTimeOperations.DaysBetweenDates(2024, 1, 1, 2024, 12, 31)}");
// // DaysBetweenDates - mixed
// Console.WriteLine($"[MIXED] DaysBetween(varYear,varMonth,varDay,2024,12,31): {DateTimeOperations.DaysBetweenDates(varYear, varMonth, varDay, 2024, 12, 31)}");
// // DaysBetweenDates - alleen variabelen
// Console.WriteLine($"[VARS ] DaysBetween(varYear,varMonth,varDay,varYear,varMonth+6,varDay): {DateTimeOperations.DaysBetweenDates(varYear, varMonth, varDay, varYear, varMonth + 6, varDay)}");
//
// // IsLeapYear - alleen constanten
// Console.WriteLine($"[CONST] IsLeapYear(2024): {DateTimeOperations.IsLeapYear(2024)}");
// // IsLeapYear - mixed
// Console.WriteLine($"[MIXED] IsLeapYear(varYear): {DateTimeOperations.IsLeapYear(varYear)}");
// // IsLeapYear - alleen variabelen
// Console.WriteLine($"[VARS ] IsLeapYear(varYear): {DateTimeOperations.IsLeapYear(varYear)}");
//
// // DaysInMonth - alleen constanten
// Console.WriteLine($"[CONST] DaysInMonth(2024,2): {DateTimeOperations.DaysInMonth(2024, 2)}");
// // DaysInMonth - mixed
// Console.WriteLine($"[MIXED] DaysInMonth(varYear,varMonth): {DateTimeOperations.DaysInMonth(varYear, varMonth)}");
// // DaysInMonth - alleen variabelen
// Console.WriteLine($"[VARS ] DaysInMonth(varYear,varMonth): {DateTimeOperations.DaysInMonth(varYear, varMonth)}");
//
// // GetWeekNumber - alleen constanten
// Console.WriteLine($"[CONST] GetWeekNumber(2024,6,15): {DateTimeOperations.GetWeekNumber(2024, 6, 15)}");
// // GetWeekNumber - mixed
// Console.WriteLine($"[MIXED] GetWeekNumber(varYear,varMonth,varDay): {DateTimeOperations.GetWeekNumber(varYear, varMonth, varDay)}");
// // GetWeekNumber - alleen variabelen
// Console.WriteLine($"[VARS ] GetWeekNumber(varYear,varMonth,varDay): {DateTimeOperations.GetWeekNumber(varYear, varMonth, varDay)}");
//
// // GetQuarter - alleen constanten
// Console.WriteLine($"[CONST] GetQuarter(6): {DateTimeOperations.GetQuarter(6)}");
// // GetQuarter - mixed
// Console.WriteLine($"[MIXED] GetQuarter(varMonth): {DateTimeOperations.GetQuarter(varMonth)}");
// // GetQuarter - alleen variabelen
// Console.WriteLine($"[VARS ] GetQuarter(varMonth): {DateTimeOperations.GetQuarter(varMonth)}");
//
// // DaysInYear - alleen constanten
// Console.WriteLine($"[CONST] DaysInYear(2024): {DateTimeOperations.DaysInYear(2024)}");
// // DaysInYear - mixed
// Console.WriteLine($"[MIXED] DaysInYear(varYear): {DateTimeOperations.DaysInYear(varYear)}");
// // DaysInYear - alleen variabelen
// Console.WriteLine($"[VARS ] DaysInYear(varYear): {DateTimeOperations.DaysInYear(varYear)}");
//
// // GetDayOfWeek - alleen constanten
// Console.WriteLine($"[CONST] GetDayOfWeek(2024,6,15): {DateTimeOperations.GetDayOfWeek(2024, 6, 15)}");
// // GetDayOfWeek - mixed
// Console.WriteLine($"[MIXED] GetDayOfWeek(varYear,varMonth,varDay): {DateTimeOperations.GetDayOfWeek(varYear, varMonth, varDay)}");
// // GetDayOfWeek - alleen variabelen
// Console.WriteLine($"[VARS ] GetDayOfWeek(varYear,varMonth,varDay): {DateTimeOperations.GetDayOfWeek(varYear, varMonth, varDay)}");
//
// // GetAge - alleen constanten
// Console.WriteLine($"[CONST] GetAge(2000,1,1,2024,6,15): {DateTimeOperations.GetAge(2000, 1, 1, 2024, 6, 15)}");
// // GetAge - mixed
// Console.WriteLine($"[MIXED] GetAge(2000,1,1,varYear,varMonth,varDay): {DateTimeOperations.GetAge(2000, 1, 1, varYear, varMonth, varDay)}");
// // GetAge - alleen variabelen
// Console.WriteLine($"[VARS ] GetAge(2000,varMonth,varDay,varYear,varMonth,varDay): {DateTimeOperations.GetAge(2000, varMonth, varDay, varYear, varMonth, varDay)}");
//
// // IsDateValid - alleen constanten
// Console.WriteLine($"[CONST] IsDateValid(2024,2,29): {DateTimeOperations.IsDateValid(2024, 2, 29)}");
// // IsDateValid - mixed
// Console.WriteLine($"[MIXED] IsDateValid(varYear,varMonth,varDay): {DateTimeOperations.IsDateValid(varYear, varMonth, varDay)}");
// // IsDateValid - alleen variabelen
// Console.WriteLine($"[VARS ] IsDateValid(varYear,varMonth,varDay): {DateTimeOperations.IsDateValid(varYear, varMonth, varDay)}");
//
// // DayOfYear - alleen constanten
// Console.WriteLine($"[CONST] DayOfYear(2024,6,15): {DateTimeOperations.DayOfYear(2024, 6, 15)}");
// // DayOfYear - mixed
// Console.WriteLine($"[MIXED] DayOfYear(varYear,varMonth,varDay): {DateTimeOperations.DayOfYear(varYear, varMonth, varDay)}");
// // DayOfYear - alleen variabelen
// Console.WriteLine($"[VARS ] DayOfYear(varYear,varMonth,varDay): {DateTimeOperations.DayOfYear(varYear, varMonth, varDay)}");
//
// // UnixTimestamp - alleen constanten
// Console.WriteLine($"[CONST] UnixTimestamp(2024,1,1,0,0,0): {DateTimeOperations.UnixTimestamp(2024, 1, 1, 0, 0, 0)}");
// // UnixTimestamp - mixed
// Console.WriteLine($"[MIXED] UnixTimestamp(varYear,varMonth,varDay,0,0,0): {DateTimeOperations.UnixTimestamp(varYear, varMonth, varDay, 0, 0, 0)}");
// // UnixTimestamp - alleen variabelen
// Console.WriteLine($"[VARS ] UnixTimestamp(varYear,varMonth,varDay,varInt8,0,0): {DateTimeOperations.UnixTimestamp(varYear, varMonth, varDay, varInt8, 0, 0)}");

// FromUnixTimestamp - exercise the reverse path as well
// var fromEpoch = DateTimeOperations.FromUnixTimestamp(DateTimeOperations.UnixTimestamp(2024, 6, 15, 0, 0, 0));
// Console.WriteLine($"[CONST] FromUnixTimestamp(UnixTimestamp(2024,6,15,..)): {fromEpoch:d}");
//
// Console.WriteLine();
//
// ═══════════════════════════════════════════════════════════════
// FINANCIAL OPERATIONS
// ═══════════════════════════════════════════════════════════════
//Console.WriteLine("═══ FINANCIAL OPERATIONS ═══\n");

//// CompoundInterest - alleen constanten
//Console.WriteLine($"[CONST] CompoundInterest(1000,0.05,12,5): {FinancialOperations.CompoundInterest(1000, 0.05, 12, 5):F2}");
//// CompoundInterest - mixed
//Console.WriteLine($"[MIXED] CompoundInterest(varDouble*100,0.05,12,5): {FinancialOperations.CompoundInterest(varDouble * 100, 0.05, 12, 5):F2}");
//// CompoundInterest - alleen variabelen
//Console.WriteLine($"[VARS ] CompoundInterest(varDouble5,varDouble4/10,varInt8,varInt2): {FinancialOperations.CompoundInterest(varDouble5, varDouble4 / 10, varInt8, varInt2):F2}");

//// SimpleInterest - alleen constanten
//Console.WriteLine($"[CONST] SimpleInterest(1000,0.05,3): {FinancialOperations.SimpleInterest(1000, 0.05, 3):F2}");
//// SimpleInterest - mixed
//Console.WriteLine($"[MIXED] SimpleInterest(varDouble*100,0.05,3): {FinancialOperations.SimpleInterest(varDouble * 100, 0.05, 3):F2}");
//// SimpleInterest - alleen variabelen
//Console.WriteLine($"[VARS ] SimpleInterest(varDouble5,varDouble4/10,varInt4): {FinancialOperations.SimpleInterest(varDouble5, varDouble4 / 10, varInt4):F2}");

//// MonthlyMortgagePayment - alleen constanten
//Console.WriteLine($"[CONST] MortgagePayment(200000,0.04,30): {FinancialOperations.MonthlyMortgagePayment(200000, 0.04, 30):F2}");
//// MonthlyMortgagePayment - mixed
//Console.WriteLine($"[MIXED] MortgagePayment(varDouble*1000,0.04,30): {FinancialOperations.MonthlyMortgagePayment(varDouble * 1000, 0.04, 30):F2}");
//// MonthlyMortgagePayment - alleen variabelen
//Console.WriteLine($"[VARS ] MortgagePayment(varDouble5*2000,varDouble4/10,varInt3): {FinancialOperations.MonthlyMortgagePayment(varDouble5 * 2000, varDouble4 / 10, varInt3):F2}");

//// ReturnOnInvestment - alleen constanten
//Console.WriteLine($"[CONST] ROI(1500,1000): {FinancialOperations.ReturnOnInvestment(1500, 1000):F2}");
//// ReturnOnInvestment - mixed
//Console.WriteLine($"[MIXED] ROI(varDouble*100,1000): {FinancialOperations.ReturnOnInvestment(varDouble * 100, 1000):F2}");
//// ReturnOnInvestment - alleen variabelen
//Console.WriteLine($"[VARS ] ROI(varDouble5,varDouble3*10): {FinancialOperations.ReturnOnInvestment(varDouble5, varDouble3 * 10):F2}");

//// FutureValue - alleen constanten
//Console.WriteLine($"[CONST] FutureValue(1000,0.05,10): {FinancialOperations.FutureValue(1000, 0.05, 10):F2}");
//// FutureValue - mixed
//Console.WriteLine($"[MIXED] FutureValue(varDouble*100,0.05,10): {FinancialOperations.FutureValue(varDouble * 100, 0.05, 10):F2}");
//// FutureValue - alleen variabelen
//Console.WriteLine($"[VARS ] FutureValue(varDouble5,varDouble4/10,varInt): {FinancialOperations.FutureValue(varDouble5, varDouble4 / 10, varInt):F2}");

//// PresentValue - alleen constanten
//Console.WriteLine($"[CONST] PresentValue(1500,0.05,10): {FinancialOperations.PresentValue(1500, 0.05, 10):F2}");
//// PresentValue - mixed
//Console.WriteLine($"[MIXED] PresentValue(varDouble*100,0.05,10): {FinancialOperations.PresentValue(varDouble * 100, 0.05, 10):F2}");
//// PresentValue - alleen variabelen
//Console.WriteLine($"[VARS ] PresentValue(varDouble5,varDouble4/10,varInt): {FinancialOperations.PresentValue(varDouble5, varDouble4 / 10, varInt):F2}");

//// LoanPayment - alleen constanten
//Console.WriteLine($"[CONST] LoanPayment(10000,0.05,60): {FinancialOperations.LoanPayment(10000, 0.05, 60):F2}");
//// LoanPayment - mixed
//Console.WriteLine($"[MIXED] LoanPayment(varDouble*1000,0.05,60): {FinancialOperations.LoanPayment(varDouble * 1000, 0.05, 60):F2}");
//// LoanPayment - alleen variabelen
//Console.WriteLine($"[VARS ] LoanPayment(varDouble5*100,varDouble4/10,varInt3*3): {FinancialOperations.LoanPayment(varDouble5 * 100, varDouble4 / 10, varInt3 * 3):F2}");

//// EffectiveAnnualRate - alleen constanten
//Console.WriteLine($"[CONST] EffectiveAnnualRate(0.05,12): {FinancialOperations.EffectiveAnnualRate(0.05, 12):F4}");
//// EffectiveAnnualRate - mixed
//Console.WriteLine($"[MIXED] EffectiveAnnualRate(0.05,varInt8): {FinancialOperations.EffectiveAnnualRate(0.05, varInt8):F4}");
//// EffectiveAnnualRate - alleen variabelen
//Console.WriteLine($"[VARS ] EffectiveAnnualRate(varDouble4/10,varInt8): {FinancialOperations.EffectiveAnnualRate(varDouble4 / 10, varInt8):F4}");

//// BreakEvenPoint - alleen constanten
//Console.WriteLine($"[CONST] BreakEvenPoint(5000,50,30): {FinancialOperations.BreakEvenPoint(5000, 50, 30):F2}");
//// BreakEvenPoint - mixed
//Console.WriteLine($"[MIXED] BreakEvenPoint(varDouble*1000,50,30): {FinancialOperations.BreakEvenPoint(varDouble * 1000, 50, 30):F2}");
//// BreakEvenPoint - alleen variabelen
//Console.WriteLine($"[VARS ] BreakEvenPoint(varDouble5*50,varDouble5/2,varInt3): {FinancialOperations.BreakEvenPoint(varDouble5 * 50, varDouble5 / 2, varInt3):F2}");

//// AnnuityPayment - alleen constanten
//Console.WriteLine($"[CONST] AnnuityPayment(0.05,10,1000): {FinancialOperations.AnnuityPayment(0.05, 10, 1000):F2}");
//// AnnuityPayment - mixed
//Console.WriteLine($"[MIXED] AnnuityPayment(0.05,varInt,1000): {FinancialOperations.AnnuityPayment(0.05, varInt, 1000):F2}");
//// AnnuityPayment - alleen variabelen
//Console.WriteLine($"[VARS ] AnnuityPayment(varDouble4/10,varInt,varDouble5*10): {FinancialOperations.AnnuityPayment(varDouble4 / 10, varInt, varDouble5 * 10):F2}");

//// AnnuityFutureValue - alleen constanten
//Console.WriteLine($"[CONST] AnnuityFutureValue(100,0.05,10): {FinancialOperations.AnnuityFutureValue(100, 0.05, 10):F2}");
//// AnnuityFutureValue - mixed
//Console.WriteLine($"[MIXED] AnnuityFutureValue(varDouble5,0.05,10): {FinancialOperations.AnnuityFutureValue(varDouble5, 0.05, 10):F2}");
//// AnnuityFutureValue - alleen variabelen
//Console.WriteLine($"[VARS ] AnnuityFutureValue(varDouble5,varDouble4/10,varInt): {FinancialOperations.AnnuityFutureValue(varDouble5, varDouble4 / 10, varInt):F2}");

//// ProfitMargin - alleen constanten
//Console.WriteLine($"[CONST] ProfitMargin(1000,750): {FinancialOperations.ProfitMargin(1000, 750):F2}");
//// ProfitMargin - mixed
//Console.WriteLine($"[MIXED] ProfitMargin(varDouble5*10,750): {FinancialOperations.ProfitMargin(varDouble5 * 10, 750):F2}");
//// ProfitMargin - alleen variabelen
//Console.WriteLine($"[VARS ] ProfitMargin(varDouble5*10,varDouble13*10): {FinancialOperations.ProfitMargin(varDouble5 * 10, varDouble13 * 10):F2}");

//// DividendYield - alleen constanten
//Console.WriteLine($"[CONST] DividendYield(5,100): {FinancialOperations.DividendYield(5, 100):F2}");
//// DividendYield - mixed
//Console.WriteLine($"[MIXED] DividendYield(varDouble,100): {FinancialOperations.DividendYield(varDouble, 100):F2}");
//// DividendYield - alleen variabelen
//Console.WriteLine($"[VARS ] DividendYield(varDouble,varDouble5): {FinancialOperations.DividendYield(varDouble, varDouble5):F2}");

//// EarningsPerShare - alleen constanten
//Console.WriteLine($"[CONST] EarningsPerShare(10000,1000): {FinancialOperations.EarningsPerShare(10000, 1000):F2}");
//// EarningsPerShare - mixed
//Console.WriteLine($"[MIXED] EarningsPerShare(varDouble5*100,1000): {FinancialOperations.EarningsPerShare(varDouble5 * 100, 1000):F2}");
//// EarningsPerShare - alleen variabelen
//Console.WriteLine($"[VARS ] EarningsPerShare(varDouble5*100,varDouble5*10): {FinancialOperations.EarningsPerShare(varDouble5 * 100, varDouble5 * 10):F2}");

//// PriceToEarningsRatio - alleen constanten
//Console.WriteLine($"[CONST] PriceToEarningsRatio(100,10): {FinancialOperations.PriceToEarningsRatio(100, 10):F2}");
//// PriceToEarningsRatio - mixed
//Console.WriteLine($"[MIXED] PriceToEarningsRatio(varDouble5,10): {FinancialOperations.PriceToEarningsRatio(varDouble5, 10):F2}");
//// PriceToEarningsRatio - alleen variabelen
//Console.WriteLine($"[VARS ] PriceToEarningsRatio(varDouble5,varInt): {FinancialOperations.PriceToEarningsRatio(varDouble5, varInt):F2}");

//// Depreciation - alleen constanten
//Console.WriteLine($"[CONST] Depreciation(10000,1000,10): {FinancialOperations.Depreciation(10000, 1000, 10):F2}");
//// Depreciation - mixed
//Console.WriteLine($"[MIXED] Depreciation(varDouble5*100,1000,10): {FinancialOperations.Depreciation(varDouble5 * 100, 1000, 10):F2}");
//// Depreciation - alleen variabelen
//Console.WriteLine($"[VARS ] Depreciation(varDouble5*100,varDouble5*10,varInt): {FinancialOperations.Depreciation(varDouble5 * 100, varDouble5 * 10, varInt):F2}");

//// TaxAmount - alleen constanten
//Console.WriteLine($"[CONST] TaxAmount(10000,0.21): {FinancialOperations.TaxAmount(10000, 0.21):F2}");
//// TaxAmount - mixed
//Console.WriteLine($"[MIXED] TaxAmount(varDouble5*100,0.21): {FinancialOperations.TaxAmount(varDouble5 * 100, 0.21):F2}");
//// TaxAmount - alleen variabelen
//Console.WriteLine($"[VARS ] TaxAmount(varDouble5*100,varDouble4/2): {FinancialOperations.TaxAmount(varDouble5 * 100, varDouble4 / 2):F2}");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // GEOMETRY OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ GEOMETRY OPERATIONS ═══\n");
//
//// CircleArea - alleen constanten
//Console.WriteLine($"[CONST] CircleArea(5): {GeometryOperations.CircleArea(5):F2}");
//// CircleArea - mixed
//Console.WriteLine($"[MIXED] CircleArea(varDouble): {GeometryOperations.CircleArea(varDouble):F2}");
//// CircleArea - alleen variabelen
//Console.WriteLine($"[VARS ] CircleArea(varDouble2): {GeometryOperations.CircleArea(varDouble2):F2}");

//// RectangleArea - alleen constanten
//Console.WriteLine($"[CONST] RectangleArea(10,5): {GeometryOperations.RectangleArea(10, 5):F2}");
//// RectangleArea - mixed
//Console.WriteLine($"[MIXED] RectangleArea(varDouble,5): {GeometryOperations.RectangleArea(varDouble, 5):F2}");
//// RectangleArea - alleen variabelen
//Console.WriteLine($"[VARS ] RectangleArea(varDouble3,varDouble2): {GeometryOperations.RectangleArea(varDouble3, varDouble2):F2}");

//// SphereVolume - alleen constanten
//Console.WriteLine($"[CONST] SphereVolume(3): {GeometryOperations.SphereVolume(3):F2}");
//// SphereVolume - mixed
//Console.WriteLine($"[MIXED] SphereVolume(varDouble): {GeometryOperations.SphereVolume(varDouble):F2}");
//// SphereVolume - alleen variabelen
//Console.WriteLine($"[VARS ] SphereVolume(varDouble6): {GeometryOperations.SphereVolume(varDouble6):F2}");

//// Distance2D - alleen constanten
//Console.WriteLine($"[CONST] Distance2D(0,0,3,4): {GeometryOperations.Distance2D(0, 0, 3, 4):F2}");
//// Distance2D - mixed
//Console.WriteLine($"[MIXED] Distance2D(0,0,varDouble,4): {GeometryOperations.Distance2D(0, 0, varDouble, 4):F2}");
//// Distance2D - alleen variabelen
//Console.WriteLine($"[VARS ] Distance2D(varDouble2,varDouble4,varDouble6,varDouble8): {GeometryOperations.Distance2D(varDouble2, varDouble4, varDouble6, varDouble8):F2}");

//// TriangleArea - alleen constanten
//Console.WriteLine($"[CONST] TriangleArea(5,6,90): {GeometryOperations.TriangleArea(5, 6, 90):F2}");
//// TriangleArea - mixed
//Console.WriteLine($"[MIXED] TriangleArea(varDouble,6,90): {GeometryOperations.TriangleArea(varDouble, 6, 90):F2}");
//// TriangleArea - alleen variabelen
//Console.WriteLine($"[VARS ] TriangleArea(varDouble2,varDouble6,varDouble12): {GeometryOperations.TriangleArea(varDouble2, varDouble6, varDouble12):F2}");

//// CircleCircumference - alleen constanten
//Console.WriteLine($"[CONST] CircleCircumference(5): {GeometryOperations.CircleCircumference(5):F2}");
//// CircleCircumference - mixed
//Console.WriteLine($"[MIXED] CircleCircumference(varDouble): {GeometryOperations.CircleCircumference(varDouble):F2}");
//// CircleCircumference - alleen variabelen
//Console.WriteLine($"[VARS ] CircleCircumference(varDouble2): {GeometryOperations.CircleCircumference(varDouble2):F2}");

//// RectanglePerimeter - alleen constanten
//Console.WriteLine($"[CONST] RectanglePerimeter(10,5): {GeometryOperations.RectanglePerimeter(10, 5):F2}");
//// RectanglePerimeter - mixed
//Console.WriteLine($"[MIXED] RectanglePerimeter(varDouble,5): {GeometryOperations.RectanglePerimeter(varDouble, 5):F2}");
//// RectanglePerimeter - alleen variabelen
//Console.WriteLine($"[VARS ] RectanglePerimeter(varDouble3,varDouble2): {GeometryOperations.RectanglePerimeter(varDouble3, varDouble2):F2}");

//// SphereSurfaceArea - alleen constanten
//Console.WriteLine($"[CONST] SphereSurfaceArea(3): {GeometryOperations.SphereSurfaceArea(3):F2}");
//// SphereSurfaceArea - mixed
//Console.WriteLine($"[MIXED] SphereSurfaceArea(varDouble): {GeometryOperations.SphereSurfaceArea(varDouble):F2}");
//// SphereSurfaceArea - alleen variabelen
//Console.WriteLine($"[VARS ] SphereSurfaceArea(varDouble6): {GeometryOperations.SphereSurfaceArea(varDouble6):F2}");

//// CylinderVolume - alleen constanten
//Console.WriteLine($"[CONST] CylinderVolume(3,5): {GeometryOperations.CylinderVolume(3, 5):F2}");
//// CylinderVolume - mixed
//Console.WriteLine($"[MIXED] CylinderVolume(varDouble,5): {GeometryOperations.CylinderVolume(varDouble, 5):F2}");
//// CylinderVolume - alleen variabelen
//Console.WriteLine($"[VARS ] CylinderVolume(varDouble2,varDouble6): {GeometryOperations.CylinderVolume(varDouble2, varDouble6):F2}");

//// TriangleAreaHeron - alleen constanten
//Console.WriteLine($"[CONST] TriangleAreaHeron(3,4,5): {GeometryOperations.TriangleAreaHeron(3, 4, 5):F2}");
//// TriangleAreaHeron - mixed
//Console.WriteLine($"[MIXED] TriangleAreaHeron(varDouble,4,5): {GeometryOperations.TriangleAreaHeron(varDouble, 4, 5):F2}");
//// TriangleAreaHeron - alleen variabelen
//Console.WriteLine($"[VARS ] TriangleAreaHeron(varDouble2,varDouble6,varDouble7): {GeometryOperations.TriangleAreaHeron(varDouble2, varDouble6, varDouble7):F2}");

//// Distance3D - alleen constanten
//Console.WriteLine($"[CONST] Distance3D(0,0,0,1,2,2): {GeometryOperations.Distance3D(0, 0, 0, 1, 2, 2):F2}");
//// Distance3D - mixed
//Console.WriteLine($"[MIXED] Distance3D(0,0,0,varDouble,2,2): {GeometryOperations.Distance3D(0, 0, 0, varDouble, 2, 2):F2}");
//// Distance3D - alleen variabelen
//Console.WriteLine($"[VARS ] Distance3D(varDouble2,varDouble4,varDouble6,varDouble3,varDouble7,varDouble8): {GeometryOperations.Distance3D(varDouble2, varDouble4, varDouble6, varDouble3, varDouble7, varDouble8):F2}");

//// ManhattanDistance2D - alleen constanten
//Console.WriteLine($"[CONST] ManhattanDistance2D(0,0,3,4): {GeometryOperations.ManhattanDistance2D(0, 0, 3, 4):F2}");
//// ManhattanDistance2D - mixed
//Console.WriteLine($"[MIXED] ManhattanDistance2D(0,0,varDouble,4): {GeometryOperations.ManhattanDistance2D(0, 0, varDouble, 4):F2}");
//// ManhattanDistance2D - alleen variabelen
//Console.WriteLine($"[VARS ] ManhattanDistance2D(varDouble2,varDouble4,varDouble6,varDouble8): {GeometryOperations.ManhattanDistance2D(varDouble2, varDouble4, varDouble6, varDouble8):F2}");

//// CylinderSurfaceArea - alleen constanten
//Console.WriteLine($"[CONST] CylinderSurfaceArea(3,5): {GeometryOperations.CylinderSurfaceArea(3, 5):F2}");
//// CylinderSurfaceArea - mixed
//Console.WriteLine($"[MIXED] CylinderSurfaceArea(varDouble,5): {GeometryOperations.CylinderSurfaceArea(varDouble, 5):F2}");
//// CylinderSurfaceArea - alleen variabelen
//Console.WriteLine($"[VARS ] CylinderSurfaceArea(varDouble2,varDouble6): {GeometryOperations.CylinderSurfaceArea(varDouble2, varDouble6):F2}");

//// ConeVolume - alleen constanten
//Console.WriteLine($"[CONST] ConeVolume(3,5): {GeometryOperations.ConeVolume(3, 5):F2}");
//// ConeVolume - mixed
//Console.WriteLine($"[MIXED] ConeVolume(varDouble,5): {GeometryOperations.ConeVolume(varDouble, 5):F2}");
//// ConeVolume - alleen variabelen
//Console.WriteLine($"[VARS ] ConeVolume(varDouble2,varDouble6): {GeometryOperations.ConeVolume(varDouble2, varDouble6):F2}");

//// PolygonArea - alleen constanten
//Console.WriteLine($"[CONST] PolygonArea(0,0,4,0,4,3,0,3): {GeometryOperations.PolygonArea(0, 0, 4, 0, 4, 3, 0, 3):F2}");
//// PolygonArea - mixed
//Console.WriteLine($"[MIXED] PolygonArea(0,0,varDouble,0,varDouble,varDouble,0,varDouble): {GeometryOperations.PolygonArea(0, 0, varDouble, 0, varDouble, varDouble, 0, varDouble):F2}");
//// PolygonArea - alleen variabelen
//Console.WriteLine($"[VARS ] PolygonArea(0,0,varDouble6,0,varDouble6,varDouble4,0,varDouble4): {GeometryOperations.PolygonArea(0, 0, varDouble6, 0, varDouble6, varDouble4, 0, varDouble4):F2}");

//// MidPoint2D - alleen constanten
//var mid1 = GeometryOperations.MidPoint2D(0, 0, 10, 10);
//Console.WriteLine($"[CONST] MidPoint2D(0,0,10,10): ({mid1.x:F2},{mid1.y:F2})");
//// MidPoint2D - mixed
//var mid2 = GeometryOperations.MidPoint2D(0, 0, varDouble, 10);
//Console.WriteLine($"[MIXED] MidPoint2D(0,0,varDouble,10): ({mid2.x:F2},{mid2.y:F2})");
//// MidPoint2D - alleen variabelen
//var mid3 = GeometryOperations.MidPoint2D(varDouble2, varDouble4, varDouble6, varDouble8);
//Console.WriteLine($"[VARS ] MidPoint2D(varDouble2,varDouble4,varDouble6,varDouble8): ({mid3.x:F2},{mid3.y:F2})");

//// AngleBetweenVectors2D - alleen constanten
//Console.WriteLine($"[CONST] AngleBetweenVectors2D(1,0,0,1): {GeometryOperations.AngleBetweenVectors2D(1, 0, 0, 1):F2}");
//// AngleBetweenVectors2D - mixed
//Console.WriteLine($"[MIXED] AngleBetweenVectors2D(varDouble,0,0,1): {GeometryOperations.AngleBetweenVectors2D(varDouble, 0, 0, 1):F2}");
//// AngleBetweenVectors2D - alleen variabelen
//Console.WriteLine($"[VARS ] AngleBetweenVectors2D(varDouble2,varDouble4,varDouble6,varDouble8): {GeometryOperations.AngleBetweenVectors2D(varDouble2, varDouble4, varDouble6, varDouble8):F2}");

//// IsPointInRectangle - alleen constanten
//Console.WriteLine($"[CONST] IsPointInRectangle(5,5,0,0,10,10): {GeometryOperations.IsPointInRectangle(5, 5, 0, 0, 10, 10)}");
//// IsPointInRectangle - mixed
//Console.WriteLine($"[MIXED] IsPointInRectangle(varDouble,varDouble,0,0,10,10): {GeometryOperations.IsPointInRectangle(varDouble, varDouble, 0, 0, 10, 10)}");
//// IsPointInRectangle - alleen variabelen
//Console.WriteLine($"[VARS ] IsPointInRectangle(varDouble2,varDouble4,0,0,varInt,varInt): {GeometryOperations.IsPointInRectangle(varDouble2, varDouble4, 0, 0, varInt, varInt)}");

//// IsPointInCircle - alleen constanten
//Console.WriteLine($"[CONST] IsPointInCircle(3,4,0,0,5): {GeometryOperations.IsPointInCircle(3, 4, 0, 0, 5)}");
//// IsPointInCircle - mixed
//Console.WriteLine($"[MIXED] IsPointInCircle(varDouble,4,0,0,5): {GeometryOperations.IsPointInCircle(varDouble, 4, 0, 0, 5)}");
//// IsPointInCircle - alleen variabelen
//Console.WriteLine($"[VARS ] IsPointInCircle(varDouble2,varDouble4,0,0,varDouble): {GeometryOperations.IsPointInCircle(varDouble2, varDouble4, 0, 0, varDouble)}");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // PHYSICS OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ PHYSICS OPERATIONS ═══\n");
//
// // ProjectileMaxHeight - alleen constanten
// Console.WriteLine($"[CONST] ProjectileMaxHeight(20,45,9.81): {PhysicsOperations.ProjectileMaxHeight(20, 45, 9.81):F2}");
// // ProjectileMaxHeight - mixed
// Console.WriteLine($"[MIXED] ProjectileMaxHeight(varDouble*5,45,9.81): {PhysicsOperations.ProjectileMaxHeight(varDouble * 5, 45, 9.81):F2}");
// // ProjectileMaxHeight - alleen variabelen
// Console.WriteLine($"[VARS ] ProjectileMaxHeight(varDouble3*2,varDouble12/2,varDouble3): {PhysicsOperations.ProjectileMaxHeight(varDouble3 * 2, varDouble12 / 2, varDouble3):F2}");
//
// // KineticEnergy - alleen constanten
// Console.WriteLine($"[CONST] KineticEnergy(10,5): {PhysicsOperations.KineticEnergy(10, 5):F2}");
// // KineticEnergy - mixed
// Console.WriteLine($"[MIXED] KineticEnergy(varDouble*2,5): {PhysicsOperations.KineticEnergy(varDouble * 2, 5):F2}");
// // KineticEnergy - alleen variabelen
// Console.WriteLine($"[VARS ] KineticEnergy(varDouble3,varDouble2): {PhysicsOperations.KineticEnergy(varDouble3, varDouble2):F2}");
//
// // Force - alleen constanten
// Console.WriteLine($"[CONST] Force(10,9.81): {PhysicsOperations.Force(10, 9.81):F2}");
// // Force - mixed
// Console.WriteLine($"[MIXED] Force(varDouble*2,9.81): {PhysicsOperations.Force(varDouble * 2, 9.81):F2}");
// // Force - alleen variabelen
// Console.WriteLine($"[VARS ] Force(varDouble3,varDouble3): {PhysicsOperations.Force(varDouble3, varDouble3):F2}");
//
// // Momentum - alleen constanten
// Console.WriteLine($"[CONST] Momentum(5,10): {PhysicsOperations.Momentum(5, 10):F2}");
// // Momentum - mixed
// Console.WriteLine($"[MIXED] Momentum(varDouble,10): {PhysicsOperations.Momentum(varDouble, 10):F2}");
// // Momentum - alleen variabelen
// Console.WriteLine($"[VARS ] Momentum(varDouble2,varDouble3): {PhysicsOperations.Momentum(varDouble2, varDouble3):F2}");
//
// // ProjectileRange - alleen constanten
// Console.WriteLine($"[CONST] ProjectileRange(20,45,9.81): {PhysicsOperations.ProjectileRange(20, 45, 9.81):F2}");
// // ProjectileRange - mixed
// Console.WriteLine($"[MIXED] ProjectileRange(varDouble*5,45,9.81): {PhysicsOperations.ProjectileRange(varDouble * 5, 45, 9.81):F2}");
// // ProjectileRange - alleen variabelen
// Console.WriteLine($"[VARS ] ProjectileRange(varDouble3*2,varDouble12/2,varDouble3): {PhysicsOperations.ProjectileRange(varDouble3 * 2, varDouble12 / 2, varDouble3):F2}");
//
// // PotentialEnergy - alleen constanten
// Console.WriteLine($"[CONST] PotentialEnergy(10,5,9.81): {PhysicsOperations.PotentialEnergy(10, 5, 9.81):F2}");
// // PotentialEnergy - mixed
// Console.WriteLine($"[MIXED] PotentialEnergy(varDouble*2,5,9.81): {PhysicsOperations.PotentialEnergy(varDouble * 2, 5, 9.81):F2}");
// // PotentialEnergy - alleen variabelen
// Console.WriteLine($"[VARS ] PotentialEnergy(varDouble3,varDouble2,varDouble3): {PhysicsOperations.PotentialEnergy(varDouble3, varDouble2, varDouble3):F2}");
//
// // Work - alleen constanten
// Console.WriteLine($"[CONST] Work(100,10,0): {PhysicsOperations.Work(100, 10, 0):F2}");
// // Work - mixed
// Console.WriteLine($"[MIXED] Work(varDouble*20,10,0): {PhysicsOperations.Work(varDouble * 20, 10, 0):F2}");
// // Work - alleen variabelen
// Console.WriteLine($"[VARS ] Work(varDouble5,varDouble3,varDouble12): {PhysicsOperations.Work(varDouble5, varDouble3, varDouble12):F2}");
//
// // Power - alleen constanten
// Console.WriteLine($"[CONST] Power(1000,5): {PhysicsOperations.Power(1000, 5):F2}");
// // Power - mixed
// Console.WriteLine($"[MIXED] Power(varDouble*100,5): {PhysicsOperations.Power(varDouble * 100, 5):F2}");
// // Power - alleen variabelen
// Console.WriteLine($"[VARS ] Power(varDouble5,varDouble2): {PhysicsOperations.Power(varDouble5, varDouble2):F2}");
//
// // CentripetalForce - alleen constanten
// Console.WriteLine($"[CONST] CentripetalForce(10,5,2): {PhysicsOperations.CentripetalForce(10, 5, 2):F2}");
// // CentripetalForce - mixed
// Console.WriteLine($"[MIXED] CentripetalForce(varDouble*2,5,2): {PhysicsOperations.CentripetalForce(varDouble * 2, 5, 2):F2}");
// // CentripetalForce - alleen variabelen
// Console.WriteLine($"[VARS ] CentripetalForce(varDouble3,varDouble2,varDouble2): {PhysicsOperations.CentripetalForce(varDouble3, varDouble2, varDouble2):F2}");
//
// // Wavelength - alleen constanten
// Console.WriteLine($"[CONST] Wavelength(340,440): {PhysicsOperations.Wavelength(340, 440):F2}");
// // Wavelength - mixed
// Console.WriteLine($"[MIXED] Wavelength(340,varDouble*80): {PhysicsOperations.Wavelength(340, varDouble * 80):F2}");
// // Wavelength - alleen variabelen
// Console.WriteLine($"[VARS ] Wavelength(varDouble5*3,varDouble5*4): {PhysicsOperations.Wavelength(varDouble5 * 3, varDouble5 * 4):F2}");
//
// // ProjectileTimeOfFlight - alleen constanten
// Console.WriteLine($"[CONST] ProjectileTimeOfFlight(20,45,9.81): {PhysicsOperations.ProjectileTimeOfFlight(20, 45, 9.81):F2}");
// // ProjectileTimeOfFlight - mixed
// Console.WriteLine($"[MIXED] ProjectileTimeOfFlight(varDouble*5,45,9.81): {PhysicsOperations.ProjectileTimeOfFlight(varDouble * 5, 45, 9.81):F2}");
// // ProjectileTimeOfFlight - alleen variabelen
// Console.WriteLine($"[VARS ] ProjectileTimeOfFlight(varDouble3*2,varDouble12/2,varDouble3): {PhysicsOperations.ProjectileTimeOfFlight(varDouble3 * 2, varDouble12 / 2, varDouble3):F2}");
//
// // Impulse - alleen constanten
// Console.WriteLine($"[CONST] Impulse(100,5): {PhysicsOperations.Impulse(100, 5):F2}");
// // Impulse - mixed
// Console.WriteLine($"[MIXED] Impulse(varDouble*20,5): {PhysicsOperations.Impulse(varDouble * 20, 5):F2}");
// // Impulse - alleen variabelen
// Console.WriteLine($"[VARS ] Impulse(varDouble5,varDouble2): {PhysicsOperations.Impulse(varDouble5, varDouble2):F2}");
//
// // CentripetalAcceleration - alleen constanten
// Console.WriteLine($"[CONST] CentripetalAcceleration(10,2): {PhysicsOperations.CentripetalAcceleration(10, 2):F2}");
// // CentripetalAcceleration - mixed
// Console.WriteLine($"[MIXED] CentripetalAcceleration(varDouble*2,2): {PhysicsOperations.CentripetalAcceleration(varDouble * 2, 2):F2}");
// // CentripetalAcceleration - alleen variabelen
// Console.WriteLine($"[VARS ] CentripetalAcceleration(varDouble3,varDouble2): {PhysicsOperations.CentripetalAcceleration(varDouble3, varDouble2):F2}");
//
// // Frequency - alleen constanten
// Console.WriteLine($"[CONST] Frequency(0.5): {PhysicsOperations.Frequency(0.5):F2}");
// // Frequency - mixed
// Console.WriteLine($"[MIXED] Frequency(varDouble4): {PhysicsOperations.Frequency(varDouble4):F2}");
// // Frequency - alleen variabelen
// Console.WriteLine($"[VARS ] Frequency(varDouble4): {PhysicsOperations.Frequency(varDouble4):F2}");
//
// // Period - alleen constanten
// Console.WriteLine($"[CONST] Period(2): {PhysicsOperations.Period(2):F2}");
// // Period - mixed
// Console.WriteLine($"[MIXED] Period(varDouble): {PhysicsOperations.Period(varDouble):F2}");
// // Period - alleen variabelen
// Console.WriteLine($"[VARS ] Period(varDouble2): {PhysicsOperations.Period(varDouble2):F2}");
//
// // AngularVelocity - alleen constanten
// Console.WriteLine($"[CONST] AngularVelocity(10,2): {PhysicsOperations.AngularVelocity(10, 2):F2}");
// // AngularVelocity - mixed
// Console.WriteLine($"[MIXED] AngularVelocity(varDouble*2,2): {PhysicsOperations.AngularVelocity(varDouble * 2, 2):F2}");
// // AngularVelocity - alleen variabelen
// Console.WriteLine($"[VARS ] AngularVelocity(varDouble3,varDouble2): {PhysicsOperations.AngularVelocity(varDouble3, varDouble2):F2}");
//
// // DopplerEffect - alleen constanten
// Console.WriteLine($"[CONST] DopplerEffect(440,340,0,0): {PhysicsOperations.DopplerEffect(440, 340, 0, 0):F2}");
// // DopplerEffect - mixed
// Console.WriteLine($"[MIXED] DopplerEffect(varDouble*80,340,0,0): {PhysicsOperations.DopplerEffect(varDouble * 80, 340, 0, 0):F2}");
// // DopplerEffect - alleen variabelen
// Console.WriteLine($"[VARS ] DopplerEffect(varDouble5*4,varDouble5*3,varInt,varInt2): {PhysicsOperations.DopplerEffect(varDouble5 * 4, varDouble5 * 3, varInt, varInt2):F2}");
//
// // ElasticPotentialEnergy - alleen constanten
// Console.WriteLine($"[CONST] ElasticPotentialEnergy(100,0.5): {PhysicsOperations.ElasticPotentialEnergy(100, 0.5):F2}");
// // ElasticPotentialEnergy - mixed
// Console.WriteLine($"[MIXED] ElasticPotentialEnergy(varDouble5,0.5): {PhysicsOperations.ElasticPotentialEnergy(varDouble5, 0.5):F2}");
// // ElasticPotentialEnergy - alleen variabelen
// Console.WriteLine($"[VARS ] ElasticPotentialEnergy(varDouble5,varDouble4): {PhysicsOperations.ElasticPotentialEnergy(varDouble5, varDouble4):F2}");
//
// // Pressure - alleen constanten
// Console.WriteLine($"[CONST] Pressure(1000,2): {PhysicsOperations.Pressure(1000, 2):F2}");
// // Pressure - mixed
// Console.WriteLine($"[MIXED] Pressure(varDouble5*10,2): {PhysicsOperations.Pressure(varDouble5 * 10, 2):F2}");
// // Pressure - alleen variabelen
// Console.WriteLine($"[VARS ] Pressure(varDouble5,varDouble2): {PhysicsOperations.Pressure(varDouble5, varDouble2):F2}");
//
// // Density - alleen constanten
// Console.WriteLine($"[CONST] Density(1000,2): {PhysicsOperations.Density(1000, 2):F2}");
// // Density - mixed
// Console.WriteLine($"[MIXED] Density(varDouble5*10,2): {PhysicsOperations.Density(varDouble5 * 10, 2):F2}");
// // Density - alleen variabelen
// Console.WriteLine($"[VARS ] Density(varDouble5,varDouble2): {PhysicsOperations.Density(varDouble5, varDouble2):F2}");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // STRING OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ STRING OPERATIONS ═══\n");
//
// // InterpolationTest - alleen constanten
// Console.WriteLine($"[CONST] Interpolation('John',25,180): {StringOperations.InterpolationTest("John", 25, 180)}");
// // InterpolationTest - mixed
// Console.WriteLine($"[MIXED] Interpolation(varString,varInt,180): {StringOperations.InterpolationTest(varString, varInt, 180)}");
// // InterpolationTest - alleen variabelen
// Console.WriteLine($"[VARS ] Interpolation(varString,varInt,varDouble5): {StringOperations.InterpolationTest(varString, varInt, varDouble5)}");
//
// // FormatFullName - alleen constanten
// Console.WriteLine($"[CONST] FormatFullName('John','Q','Doe',true): {StringOperations.FormatFullName("John", "Q", "Doe", true)}");
// // FormatFullName - mixed
// Console.WriteLine($"[MIXED] FormatFullName('John','Q',varString,false): {StringOperations.FormatFullName("John", "Q", varString, false)}");
// // FormatFullName - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] FormatFullName('Jane','M','Smith',true): {StringOperations.FormatFullName("Jane", "M", "Smith", true)}");
//
// // Reverse - alleen constanten
// Console.WriteLine($"[CONST] Reverse('Hello'): {StringOperations.Reverse("Hello")}");
// // Reverse - mixed
// Console.WriteLine($"[MIXED] Reverse(varString): {StringOperations.Reverse(varString)}");
// // Reverse - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] Reverse('World'): {StringOperations.Reverse("World")}");
//
// // IsPalindrome - alleen constanten
// Console.WriteLine($"[CONST] IsPalindrome('racecar'): {StringOperations.IsPalindrome("racecar")}");
// // IsPalindrome - mixed
// Console.WriteLine($"[MIXED] IsPalindrome(varString): {StringOperations.IsPalindrome(varString)}");
// // IsPalindrome - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] IsPalindrome('level'): {StringOperations.IsPalindrome("level")}");
//
// // CountOccurrences - alleen constanten
// Console.WriteLine($"[CONST] CountOccurrences('hello world','l'): {StringOperations.CountOccurrences("hello world", "l")}");
// // CountOccurrences - mixed
// Console.WriteLine($"[MIXED] CountOccurrences(varString,'e'): {StringOperations.CountOccurrences(varString, "e")}");
// // CountOccurrences - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] CountOccurrences('banana','a'): {StringOperations.CountOccurrences("banana", "a")}");
//
// // RemoveWhitespace - alleen constanten
// Console.WriteLine($"[CONST] RemoveWhitespace('hello world'): {StringOperations.RemoveWhitespace("hello world")}");
// // RemoveWhitespace - mixed
// Console.WriteLine($"[MIXED] RemoveWhitespace('hello '+varString): {StringOperations.RemoveWhitespace("hello " + varString)}");
// // RemoveWhitespace - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] RemoveWhitespace('test string'): {StringOperations.RemoveWhitespace("test string")}");
//
// // ToCamelCase - alleen constanten
// Console.WriteLine($"[CONST] ToCamelCase('hello world test'): {StringOperations.ToCamelCase("hello world test")}");
// // ToCamelCase - mixed (using string literals)
// Console.WriteLine($"[MIXED] ToCamelCase('test string value'): {StringOperations.ToCamelCase("test string value")}");
// // ToCamelCase - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] ToCamelCase('some_snake_case'): {StringOperations.ToCamelCase("some_snake_case")}");
//
// // ToPascalCase - alleen constanten
// Console.WriteLine($"[CONST] ToPascalCase('hello world'): {StringOperations.ToPascalCase("hello world")}");
// // ToPascalCase - mixed (using string literals)
// Console.WriteLine($"[MIXED] ToPascalCase('my test string'): {StringOperations.ToPascalCase("my test string")}");
// // ToPascalCase - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] ToPascalCase('another_test_case'): {StringOperations.ToPascalCase("another_test_case")}");
//
// // ToSnakeCase - alleen constanten
// Console.WriteLine($"[CONST] ToSnakeCase('Hello World'): {StringOperations.ToSnakeCase("Hello World")}");
// // ToSnakeCase - mixed (using string literals)
// Console.WriteLine($"[MIXED] ToSnakeCase('My Test String'): {StringOperations.ToSnakeCase("My Test String")}");
// // ToSnakeCase - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] ToSnakeCase('Another Test'): {StringOperations.ToSnakeCase("Another Test")}");
//
// // GenerateSlug - alleen constanten
// Console.WriteLine($"[CONST] GenerateSlug('Hello World',20,'-',true): {StringOperations.GenerateSlug("Hello World", 20, '-', true)}");
// // GenerateSlug - mixed (using string literals and variables)
// Console.WriteLine($"[MIXED] GenerateSlug('Test String',varInt*2,'-',true): {StringOperations.GenerateSlug("Test String", varInt * 2, '-', true)}");
// // GenerateSlug - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] GenerateSlug('My Blog Post',varInt3*5,'_',false): {StringOperations.GenerateSlug("My Blog Post", varInt3 * 5, '_', false)}");
//
// // ToKebabCase - alleen constanten
// Console.WriteLine($"[CONST] ToKebabCase('Hello World'): {StringOperations.ToKebabCase("Hello World")}");
// // ToKebabCase - mixed (using string literals)
// Console.WriteLine($"[MIXED] ToKebabCase('My Test String'): {StringOperations.ToKebabCase("My Test String")}");
// // ToKebabCase - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] ToKebabCase('Another Test'): {StringOperations.ToKebabCase("Another Test")}");
//
// // Truncate - alleen constanten
// Console.WriteLine($"[CONST] Truncate('This is a long string',10): {StringOperations.Truncate("This is a long string", 10)}");
// // Truncate - mixed (using string literals and variables)
// Console.WriteLine($"[MIXED] Truncate('Another long string',varInt): {StringOperations.Truncate("Another long string", varInt)}");
// // Truncate - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] Truncate('Yet another string',varInt5,'***'): {StringOperations.Truncate("Yet another string", varInt5, "***")}");
//
// // RepeatString - alleen constanten
// Console.WriteLine($"[CONST] RepeatString('Ab',5): {StringOperations.RepeatString("Ab", 5)}");
// // RepeatString - mixed (using string literals and variables)
// Console.WriteLine($"[MIXED] RepeatString('X',varInt): {StringOperations.RepeatString("X", varInt)}");
// // RepeatString - alleen variabelen (using string literals)
// Console.WriteLine($"[VARS ] RepeatString('*',varInt4): {StringOperations.RepeatString("*", varInt4)}");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // COLOR OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ COLOR OPERATIONS ═══\n");
//
// HslToRgb - alleen constanten
var rgb1 = ColorOperations.HslToRgb(120, 1.0f, 0.5f);
Console.WriteLine($"[CONST] HslToRgb(120,1.0,0.5): ({rgb1.r},{rgb1.g},{rgb1.b})");
// HslToRgb - mixed
var rgb2 = ColorOperations.HslToRgb((float)varDouble * 10, 1.0f, 0.5f);
Console.WriteLine($"[MIXED] HslToRgb(varDouble*10,1.0,0.5): ({rgb2.r},{rgb2.g},{rgb2.b})");
// HslToRgb - alleen variabelen
var rgb3 = ColorOperations.HslToRgb((float)varDouble12, (float)varDouble4, (float)varDouble4);
Console.WriteLine($"[VARS ] HslToRgb(varDouble12,varDouble4,varDouble4): ({rgb3.r},{rgb3.g},{rgb3.b})");

var rgb4 = ColorOperations.HslToRgb(80, (float) varDouble4, (float) varDouble4);
Console.WriteLine($"[VARS ] HslToRgb(80,varDouble4,varDouble4): ({rgb3.r},{rgb3.g},{rgb3.b})");

// Luminance - alleen constanten
Console.WriteLine($"[CONST] Luminance(255,128,64): {ColorOperations.Luminance(255, 128, 64):F3}");
// Luminance - mixed
Console.WriteLine($"[MIXED] Luminance(varByte,128,64): {ColorOperations.Luminance(varByte, 128, 64):F3}");
// Luminance - alleen variabelen
Console.WriteLine($"[VARS ] Luminance(varByte,varByte,varByte): {ColorOperations.Luminance(varByte, varByte, varByte):F3}");

// // ContrastRatio - alleen constanten
// Console.WriteLine($"[CONST] ContrastRatio(255,255,255,0,0,0): {ColorOperations.ContrastRatio(255, 255, 255, 0, 0, 0):F2}");
// // ContrastRatio - mixed
// Console.WriteLine($"[MIXED] ContrastRatio(varByte,varByte,varByte,0,0,0): {ColorOperations.ContrastRatio(varByte, varByte, varByte, 0, 0, 0):F2}");
// // ContrastRatio - alleen variabelen
// Console.WriteLine($"[VARS ] ContrastRatio(varByte,varByte,varByte,varByte,varByte,varByte): {ColorOperations.ContrastRatio(varByte, varByte, varByte, varByte, varByte, varByte):F2}");

// RgbToHsl - alleen constanten
var hsl1 = ColorOperations.RgbToHsl(255, 0, 0);
Console.WriteLine($"[CONST] RgbToHsl(255,0,0): ({hsl1.h:F2},{hsl1.s:F2},{hsl1.l:F2})");
// RgbToHsl - mixed
var hsl2 = ColorOperations.RgbToHsl(varByte, 128, 64);
Console.WriteLine($"[MIXED] RgbToHsl(varByte,128,64): ({hsl2.h:F2},{hsl2.s:F2},{hsl2.l:F2})");
// RgbToHsl - alleen variabelen
var hsl3 = ColorOperations.RgbToHsl(varByte, varByte, varByte);
Console.WriteLine($"[VARS ] RgbToHsl(varByte,varByte,varByte): ({hsl3.h:F2},{hsl3.s:F2},{hsl3.l:F2})");

// BlendRgb - alleen constanten
var blend1 = ColorOperations.BlendRgb(255, 255, 255, 0, 0, 0, 0.5f, false);
Console.WriteLine($"[CONST] BlendRgb(255,255,255,0,0,0,0.5,false): ({blend1.r},{blend1.g},{blend1.b})");
// BlendRgb - mixed
var blend2 = ColorOperations.BlendRgb(varByte, varByte, varByte, 0, 0, 0, 0.5f, false);
Console.WriteLine($"[MIXED] BlendRgb(varByte,varByte,varByte,0,0,0,0.5,false): ({blend2.r},{blend2.g},{blend2.b})");
// BlendRgb - alleen variabelen
var blend3 = ColorOperations.BlendRgb(varByte, varByte, varByte, varByte, varByte, varByte, (float)varDouble4, false);
Console.WriteLine($"[VARS ] BlendRgb(varByte,varByte,varByte,varByte,varByte,varByte,varDouble4,false): ({blend3.r},{blend3.g},{blend3.b})");

// RgbToGrayscale - alleen constanten
var gray1 = ColorOperations.RgbToGrayscale(100, 150, 200);
Console.WriteLine($"[CONST] RgbToGrayscale(100,150,200): ({gray1.r},{gray1.g},{gray1.b})");
// RgbToGrayscale - mixed
var gray2 = ColorOperations.RgbToGrayscale(varByte, 150, 200);
Console.WriteLine($"[MIXED] RgbToGrayscale(varByte,150,200): ({gray2.r},{gray2.g},{gray2.b})");
// RgbToGrayscale - alleen variabelen
var gray3 = ColorOperations.RgbToGrayscale(varByte, varByte, varByte);
Console.WriteLine($"[VARS ] RgbToGrayscale(varByte,varByte,varByte): ({gray3.r},{gray3.g},{gray3.b})");

// InvertRgb - alleen constanten
var inv1 = ColorOperations.InvertRgb(100, 150, 200);
Console.WriteLine($"[CONST] InvertRgb(100,150,200): ({inv1.r},{inv1.g},{inv1.b})");
// InvertRgb - mixed
var inv2 = ColorOperations.InvertRgb(varByte, 150, 200);
Console.WriteLine($"[MIXED] InvertRgb(varByte,150,200): ({inv2.r},{inv2.g},{inv2.b})");
// InvertRgb - alleen variabelen
var inv3 = ColorOperations.InvertRgb(varByte, varByte, varByte);
Console.WriteLine($"[VARS ] InvertRgb(varByte,varByte,varByte): ({inv3.r},{inv3.g},{inv3.b})");

// AdjustBrightness - alleen constanten
var bright1 = ColorOperations.AdjustBrightness(100, 150, 200, 1.5f);
Console.WriteLine($"[CONST] AdjustBrightness(100,150,200,1.5): ({bright1.r},{bright1.g},{bright1.b})");
// AdjustBrightness - mixed
var bright2 = ColorOperations.AdjustBrightness(varByte, 150, 200, 1.5f);
Console.WriteLine($"[MIXED] AdjustBrightness(varByte,150,200,1.5): ({bright2.r},{bright2.g},{bright2.b})");
// AdjustBrightness - alleen variabelen
var bright3 = ColorOperations.AdjustBrightness(varByte, varByte, varByte, (float)varDouble4 * 2);
Console.WriteLine($"[VARS ] AdjustBrightness(varByte,varByte,varByte,varDouble4*2): ({bright3.r},{bright3.g},{bright3.b})");

// RgbToHex - alleen constanten
Console.WriteLine($"[CONST] RgbToHex(255,128,64): 0x{ColorOperations.RgbToHex(255, 128, 6):X6}");
// RgbToHex - mixed
Console.WriteLine($"[MIXED] RgbToHex(varByte,128,64): 0x{ColorOperations.RgbToHex(varByte, 128, 64):X6}");
// RgbToHex - alleen variabelen
Console.WriteLine($"[VARS ] RgbToHex(varByte,varByte,varByte): 0x{ColorOperations.RgbToHex(varByte, varByte, varByte):X6}");

// HexToRgb - alleen constanten
var hexrgb1 = ColorOperations.HexToRgb(0xFF8040);
Console.WriteLine($"[CONST] HexToRgb(0xFF8040): ({hexrgb1.r},{hexrgb1.g},{hexrgb1.b})");
// HexToRgb - mixed (using constant hex value)
var hexrgb2 = ColorOperations.HexToRgb(0x00FF00);
Console.WriteLine($"[MIXED] HexToRgb(0x00FF00): ({hexrgb2.r},{hexrgb2.g},{hexrgb2.b})");
// HexToRgb - alleen variabelen (using calculated value)
var hexrgb3 = ColorOperations.HexToRgb(varInt * 65536);
Console.WriteLine($"[VARS ] HexToRgb(varInt*65536): ({hexrgb3.r},{hexrgb3.g},{hexrgb3.b})");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // COLLECTION OPERATIONS
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ COLLECTION OPERATIONS ═══\n");
//
// // GetArray - alleen constanten
// Console.WriteLine($"[CONST] GetArray(1,2,3,4,5,6,7): [{String.Join(", ", CollectionOperations.GetArray(1, 2, 3, 4, 5, 6, 7))}]");
// // GetArray - mixed
// Console.WriteLine($"[MIXED] GetArray(varInt,2,3,4,5,6,7): [{String.Join(", ", CollectionOperations.GetArray(varInt, 2, 3, 4, 5, 6, 7))}]");
// // GetArray - alleen variabelen
// Console.WriteLine($"[VARS ] GetArray(varInt,varInt2,varInt4,varInt,varInt2,varInt4,varInt): [{String.Join(", ", CollectionOperations.GetArray(varInt, varInt2, varInt4, varInt, varInt2, varInt4, varInt))}]");
//
// // GenerateRange - alleen constanten
// Console.WriteLine($"[CONST] GenerateRange(0,10,2): [{String.Join(", ", CollectionOperations.GenerateRange(0, 10, 2))}]");
// // GenerateRange - mixed
// Console.WriteLine($"[MIXED] GenerateRange(0,varInt,2): [{String.Join(", ", CollectionOperations.GenerateRange(0, varInt, 2))}]");
// // GenerateRange - alleen variabelen
// Console.WriteLine($"[VARS ] GenerateRange(varInt2,varInt,varInt4): [{String.Join(", ", CollectionOperations.GenerateRange(varInt2, varInt, varInt4))}]");
//
// // FilterAndTransform - alleen constanten
// Console.WriteLine($"[CONST] FilterAndTransform([1,2,3,4,5],2,4,10): [{String.Join(", ", CollectionOperations.FilterAndTransform(new[] { 1, 2, 3, 4, 5 }, 2, 4, 10))}]");
// // FilterAndTransform - mixed
// Console.WriteLine($"[MIXED] FilterAndTransform([1,2,3,4,5],varInt2,varInt4,10): [{String.Join(", ", CollectionOperations.FilterAndTransform(new[] { 1, 2, 3, 4, 5 }, varInt2, varInt4, 10))}]");
// // FilterAndTransform - alleen variabelen
// Console.WriteLine($"[VARS ] FilterAndTransform([1,2,3,4,5],varInt2,varInt,varInt2): [{String.Join(", ", CollectionOperations.FilterAndTransform(new[] { 1, 2, 3, 4, 5 }, varInt2, varInt, varInt2))}]");
//
// // ReverseArray - alleen constanten
// Console.WriteLine($"[CONST] ReverseArray(1,2,3,4,5): [{String.Join(", ", CollectionOperations.ReverseArray(1, 2, 3, 4, 5))}]");
// // ReverseArray - mixed (using array literal)
// Console.WriteLine($"[MIXED] ReverseArray(varInt,2,3,4,5): [{String.Join(", ", CollectionOperations.ReverseArray(varInt, 2, 3, 4, 5))}]");
// // ReverseArray - alleen variabelen
// Console.WriteLine($"[VARS ] ReverseArray(varInt,varInt2,varInt4): [{String.Join(", ", CollectionOperations.ReverseArray(varInt, varInt2, varInt4))}]");
//
// // ChunkArray - alleen constanten
// Console.WriteLine($"[CONST] ChunkArray([1,2,3,4,5,6],2,1): [{String.Join(", ", CollectionOperations.ChunkArray(new[] { 1, 2, 3, 4, 5, 6 }, 2, 1))}]");
// // ChunkArray - mixed
// Console.WriteLine($"[MIXED] ChunkArray([1,2,3,4,5,6],varInt2,1): [{String.Join(", ", CollectionOperations.ChunkArray(new[] { 1, 2, 3, 4, 5, 6 }, varInt2, 1))}]");
// // ChunkArray - alleen variabelen
// Console.WriteLine($"[VARS ] ChunkArray([1,2,3,4,5,6],varInt4,varInt2): [{String.Join(", ", CollectionOperations.ChunkArray(new[] { 1, 2, 3, 4, 5, 6 }, varInt4, varInt2))}]");
//
// // RemoveDuplicates - alleen constanten
// Console.WriteLine($"[CONST] RemoveDuplicates(1,2,2,3,3,3): [{String.Join(", ", CollectionOperations.RemoveDuplicates(1, 2, 2, 3, 3, 3))}]");
// // RemoveDuplicates - mixed (using array literal)
// Console.WriteLine($"[MIXED] RemoveDuplicates(varInt,2,varInt,3): [{String.Join(", ", CollectionOperations.RemoveDuplicates(varInt, 2, varInt, 3))}]");
// // RemoveDuplicates - alleen variabelen
// Console.WriteLine($"[VARS ] RemoveDuplicates(varInt,varInt2,varInt,varInt4): [{String.Join(", ", CollectionOperations.RemoveDuplicates(varInt, varInt2, varInt, varInt4))}]");
//
// // IntersectArrays - alleen constanten
// Console.WriteLine($"[CONST] IntersectArrays([1,2,3],[2,3,4]): [{String.Join(", ", CollectionOperations.IntersectArrays(new[] { 1, 2, 3 }, new[] { 2, 3, 4 }))}]");
// // IntersectArrays - mixed
// Console.WriteLine($"[MIXED] IntersectArrays([1,2,3],[varInt2,3,4]): [{String.Join(", ", CollectionOperations.IntersectArrays(new[] { 1, 2, 3 }, new[] { varInt2, 3, 4 }))}]");
// // IntersectArrays - alleen variabelen
// Console.WriteLine($"[VARS ] IntersectArrays([varInt,varInt2],[varInt2,varInt4]): [{String.Join(", ", CollectionOperations.IntersectArrays(new[] { varInt, varInt2 }, new[] { varInt2, varInt4 }))}]");
//
// // UnionArrays - alleen constanten
// Console.WriteLine($"[CONST] UnionArrays([1,2,3],[3,4,5]): [{String.Join(", ", CollectionOperations.UnionArrays(new[] { 1, 2, 3 }, new[] { 3, 4, 5 }))}]");
// // UnionArrays - mixed
// Console.WriteLine($"[MIXED] UnionArrays([1,2,3],[varInt4,4,5]): [{String.Join(", ", CollectionOperations.UnionArrays(new[] { 1, 2, 3 }, new[] { varInt4, 4, 5 }))}]");
// // UnionArrays - alleen variabelen
// Console.WriteLine($"[VARS ] UnionArrays([varInt,varInt2],[varInt4,varInt2]): [{String.Join(", ", CollectionOperations.UnionArrays(new[] { varInt, varInt2 }, new[] { varInt4, varInt2 }))}]");
//
// // FlattenNestedArray - alleen constanten
// Console.WriteLine($"[CONST] FlattenNestedArray([[1,2],[3,4],[5]]): [{String.Join(", ", CollectionOperations.FlattenNestedArray(new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } }))}]");
// // FlattenNestedArray - mixed
// Console.WriteLine($"[MIXED] FlattenNestedArray([[varInt,2],[3,4]]): [{String.Join(", ", CollectionOperations.FlattenNestedArray(new[] { new[] { varInt, 2 }, new[] { 3, 4 } }))}]");
// // FlattenNestedArray - alleen variabelen
// Console.WriteLine($"[VARS ] FlattenNestedArray([[varInt,varInt2],[varInt4]]): [{String.Join(", ", CollectionOperations.FlattenNestedArray(new[] { new[] { varInt, varInt2 }, new[] { varInt4 } }))}]");
//
// // RotateArray - alleen constanten
// Console.WriteLine($"[CONST] RotateArray([1,2,3,4,5],2): [{String.Join(", ", CollectionOperations.RotateArray(new[] { 1, 2, 3, 4, 5 }, 2))}]");
// // RotateArray - mixed
// Console.WriteLine($"[MIXED] RotateArray([1,2,3,4,5],varInt2): [{String.Join(", ", CollectionOperations.RotateArray(new[] { 1, 2, 3, 4, 5 }, varInt2))}]");
// // RotateArray - alleen variabelen
// Console.WriteLine($"[VARS ] RotateArray([varInt,varInt2,varInt4,varInt],varInt4): [{String.Join(", ", CollectionOperations.RotateArray(new[] { varInt, varInt2, varInt4, varInt }, varInt4))}]");
//
// // FindMax - alleen constanten
// Console.WriteLine($"[CONST] FindMax(5,2,8,1,9,3): {CollectionOperations.FindMax(5, 2, 8, 1, 9, 3)}");
// // FindMax - mixed
// Console.WriteLine($"[MIXED] FindMax(varInt,2,8,1): {CollectionOperations.FindMax(varInt, 2, 8, 1)}");
// // FindMax - alleen variabelen
// Console.WriteLine($"[VARS ] FindMax(varInt,varInt2,varInt4,varInt5): {CollectionOperations.FindMax(varInt, varInt2, varInt4, varInt5)}");
//
// // FindMin - alleen constanten
// Console.WriteLine($"[CONST] FindMin(5,2,8,1,9,3): {CollectionOperations.FindMin(5, 2, 8, 1, 9, 3)}");
// // FindMin - mixed
// Console.WriteLine($"[MIXED] FindMin(varInt,2,8,1): {CollectionOperations.FindMin(varInt, 2, 8, 1)}");
// // FindMin - alleen variabelen
// Console.WriteLine($"[VARS ] FindMin(varInt,varInt2,varInt4,varInt5): {CollectionOperations.FindMin(varInt, varInt2, varInt4, varInt5)}");
//
// // BubbleSort - alleen constanten
// Console.WriteLine($"[CONST] BubbleSort(5,2,8,1,9): [{String.Join(", ", CollectionOperations.BubbleSort(5, 2, 8, 1, 9))}]");
// // BubbleSort - mixed
// Console.WriteLine($"[MIXED] BubbleSort(varInt,2,8,1): [{String.Join(", ", CollectionOperations.BubbleSort(varInt, 2, 8, 1))}]");
// // BubbleSort - alleen variabelen
// Console.WriteLine($"[VARS ] BubbleSort(varInt5,varInt2,varInt8,varInt): [{String.Join(", ", CollectionOperations.BubbleSort(varInt5, varInt2, varInt8, varInt))}]");
//
// // BinarySearch - alleen constanten
// Console.WriteLine($"[CONST] BinarySearch([1,2,3,4,5],3): {CollectionOperations.BinarySearch(new[] { 1, 2, 3, 4, 5 }, 3)}");
// // BinarySearch - mixed
// Console.WriteLine($"[MIXED] BinarySearch([1,2,3,4,5],varInt4): {CollectionOperations.BinarySearch(new[] { 1, 2, 3, 4, 5 }, varInt4)}");
// // BinarySearch - alleen variabelen
// Console.WriteLine($"[VARS ] BinarySearch([varInt2,varInt4,varInt,varInt5],varInt): {CollectionOperations.BinarySearch(new[] { varInt2, varInt4, varInt, varInt5 }, varInt)}");
//
// Console.WriteLine();
//
// // ═══════════════════════════════════════════════════════════════
// // REMAINING FUNCTIONS (previously missing) - tests added here
// // ═══════════════════════════════════════════════════════════════
// Console.WriteLine("═══ REMAINING / MISC EXTRA FUNCTIONS ═══\n");
//
// // Geometry: EllipseArea
// Console.WriteLine($"[CONST] EllipseArea(5,3): {GeometryOperations.EllipseArea(5, 3):F2}");
// Console.WriteLine($"[MIXED] EllipseArea(varDouble,3): {GeometryOperations.EllipseArea(varDouble, 3):F2}");
// Console.WriteLine($"[VARS ] EllipseArea(varDouble6,varDouble4): {GeometryOperations.EllipseArea(varDouble6, varDouble4):F2}");
//
// // Geometry: TrapezoidArea
// Console.WriteLine($"[CONST] TrapezoidArea(3,5,4): {GeometryOperations.TrapezoidArea(3, 5, 4):F2}");
// Console.WriteLine($"[MIXED] TrapezoidArea(varDouble,5,4): {GeometryOperations.TrapezoidArea(varDouble, 5, 4):F2}");
// Console.WriteLine($"[VARS ] TrapezoidArea(varDouble2,varDouble6,varDouble3): {GeometryOperations.TrapezoidArea(varDouble2, varDouble6, varDouble3):F2}");
//
// // Physics: EscapeVelocity
// Console.WriteLine($"[CONST] EscapeVelocity(6.67430e-11,5.972e24,6.371e6): {PhysicsOperations.EscapeVelocity(6.67430e-11, 5.972e24, 6.371e6):F2}");
// Console.WriteLine($"[MIXED] EscapeVelocity(6.67430e-11,varDouble5,6.371e6): {PhysicsOperations.EscapeVelocity(6.67430e-11, varDouble5, 6.371e6):F2}");
// Console.WriteLine($"[VARS ] EscapeVelocity(varDouble5,varDouble6,varDouble7): {PhysicsOperations.EscapeVelocity(varDouble5, varDouble6, varDouble7):F2}");
//
// // Physics: RelativisticMass
// Console.WriteLine($"[CONST] RelativisticMass(1.0,100000,299792458): {PhysicsOperations.RelativisticMass(1.0, 100000, 299792458):F6}");
// Console.WriteLine($"[MIXED] RelativisticMass(1.0,varDouble5,299792458): {PhysicsOperations.RelativisticMass(1.0, varDouble5, 299792458):F6}");
// Console.WriteLine($"[VARS ] RelativisticMass(varDouble5,varDouble6,299792458): {PhysicsOperations.RelativisticMass(varDouble5, varDouble6, 299792458):F6}");
//
// // Physics: SchwarzschildRadius
// Console.WriteLine($"[CONST] SchwarzschildRadius(5.972e24,6.67430e-11,299792458): {PhysicsOperations.SchwarzschildRadius(5.972e24, 6.67430e-11, 299792458):E6}");
// Console.WriteLine($"[MIXED] SchwarzschildRadius(varDouble5,6.67430e-11,299792458): {PhysicsOperations.SchwarzschildRadius(varDouble5, 6.67430e-11, 299792458):E6}");
// Console.WriteLine($"[VARS ] SchwarzschildRadius(varDouble6,varDouble7,299792458): {PhysicsOperations.SchwarzschildRadius(varDouble6, varDouble7, 299792458):E6}");
//
// // StringOperations: StringLength / StringBytes / Base64Encode / Split / LevenshteinDistance
// Console.WriteLine($"[CONST] StringLength('hello', UTF8): {StringOperations.StringLength("hello", System.Text.Encoding.UTF8)}");
// var sbBytes = StringOperations.StringBytes("hello", System.Text.Encoding.UTF8);
// Console.WriteLine($"[CONST] StringBytes('hello', UTF8): [{string.Join(", ", sbBytes.ToArray())}]");
// var b64 = StringOperations.Base64Encode("hello");
// Console.WriteLine($"[CONST] Base64Encode('hello'): {b64}");
// var split1 = StringOperations.Split("a,b,c", ',');
// Console.WriteLine($"[CONST] Split('a,b,c',','): [{string.Join(", ", split1)}]");
// Console.WriteLine($"[CONST] LevenshteinDistance('kitten','sitting'): {StringOperations.LevenshteinDistance("kitten", "sitting")}");
//
// // CollectionOperations: CountOccurrences
// var countDict = CollectionOperations.CountOccurrences(1,2,2,3,3,3);
// Console.WriteLine($"[CONST] CountOccurrences(1,2,2,3,3,3): {{{string.Join(", ", countDict.Select(kv => kv.Key + ":" + kv.Value))}}}");
//
// // MiscellaneousOperations: ToString (enum), GetNames, Waiting, RandomInRange, ClearBit, ToggleBit, Decimal/Hex conversions
// Console.WriteLine($"[CONST] EnumToString(DayOfWeek.Monday): {MiscellaneousOperations.ToString(DayOfWeek.Monday)}");
// Console.WriteLine($"[CONST] GetNames<DayOfWeek>: [{string.Join(", ", MiscellaneousOperations.GetNames<DayOfWeek>())}]");
// Console.WriteLine($"[CONST] Waiting(): {MiscellaneousOperations.Waiting().GetAwaiter().GetResult()}");
// Console.WriteLine($"[CONST] RandomInRange(1,10): {MiscellaneousOperations.RandomInRange(1, 10)}");
//
// Console.WriteLine($"[CONST] ClearBit(15,1): {MiscellaneousOperations.ClearBit(15, 1)}");
// Console.WriteLine($"[CONST] ToggleBit(8,3): {MiscellaneousOperations.ToggleBit(8, 3)}");
//
// // Add missing bit-related tests
// Console.WriteLine($"[CONST] SetBit(8,1): {MiscellaneousOperations.SetBit(8, 1)}");
// Console.WriteLine($"[CONST] CountBits(13): {MiscellaneousOperations.CountBits(13)}");
// Console.WriteLine($"[CONST] ReverseBits(1): {MiscellaneousOperations.ReverseBits(1)}");
// Console.WriteLine($"[CONST] IsBitSet(13,2): {MiscellaneousOperations.IsBitSet(13, 2)}");
// Console.WriteLine($"[CONST] NextPowerOfTwo(5): {MiscellaneousOperations.NextPowerOfTwo(5)}");
//
// Console.WriteLine($"[CONST] DecimalToBinary(13): {MiscellaneousOperations.DecimalToBinary(13)}");
// Console.WriteLine($"[CONST] BinaryToDecimal('1101'): {MiscellaneousOperations.BinaryToDecimal("1101")} ");
// Console.WriteLine($"[CONST] DecimalToHex(255): {MiscellaneousOperations.DecimalToHex(255)}");
// Console.WriteLine($"[CONST] HexToDecimal('FF'): {MiscellaneousOperations.HexToDecimal("FF")}" );
//
// Console.WriteLine();
//
// Console.WriteLine("═══ REMAINING / MISC EXTRA FUNCTIONS (PART 2) ═══\n");
//
// // CollectionOperations: IsOdd / Range
// var oddVals = CollectionOperations.IsOdd(new double[] { 1, 2, 3, 4, 5 }).ToArray();
// Console.WriteLine($"[CONST] IsOdd([1,2,3,4,5]): [{string.Join(", ", oddVals)}]");
// var rangeBytes = CollectionOperations.Range(5);
// Console.WriteLine($"[CONST] Range(5): [{string.Join(", ", rangeBytes)}]");
//
// // ColorOperations: AdjustSaturation, RgbToHsv, HsvToRgb
// var satAdjusted = ColorOperations.AdjustSaturation(120, 200, 80, 1.2f);
// Console.WriteLine($"[CONST] AdjustSaturation(120,200,80,1.2): ({satAdjusted.r},{satAdjusted.g},{satAdjusted.b})");
// var hsv = ColorOperations.RgbToHsv(255, 128, 64);
// Console.WriteLine($"[CONST] RgbToHsv(255,128,64): ({hsv.h},{hsv.s},{hsv.v})");
// var rgbFromHsv = ColorOperations.HsvToRgb(hsv.h, hsv.s, hsv.v);
// Console.WriteLine($"[CONST] HsvToRgb(...): ({rgbFromHsv.r},{rgbFromHsv.g},{rgbFromHsv.b})");
//
// // DateTimeOperations: AddBusinessDays, CountBusinessDays, GetEasterSunday
// var addBiz = DateTimeOperations.AddBusinessDays(2024, 6, 15, 10);
// Console.WriteLine($"[CONST] AddBusinessDays(2024,6,15,10): {addBiz:d}");
// Console.WriteLine($"[CONST] CountBusinessDays(2024,6,15,2024,7,1): {DateTimeOperations.CountBusinessDays(2024,6,15,2024,7,1)}");
// Console.WriteLine($"[CONST] GetEasterSunday(2025): {DateTimeOperations.GetEasterSunday(2025):d}");
//
// // FinancialOperations: NetPresentValue, InternalRateOfReturn
// Console.WriteLine($"[CONST] NetPresentValue(0.05, -1000, 300, 400, 500, 600): {FinancialOperations.NetPresentValue(0.05, -1000, 300, 400, 500, 600):F2}");
// Console.WriteLine($"[CONST] InternalRateOfReturn(-1000,300,400,500,600): {FinancialOperations.InternalRateOfReturn(-1000,300,400,500,600):F6}");
//
// // MiscellaneousOperations: many helpers
// Console.WriteLine($"[CONST] DetermineGrade(88,100,false,0): {MiscellaneousOperations.DetermineGrade(88, 100, false, 0)}");
// Console.WriteLine($"[CONST] IsEven(4): {MiscellaneousOperations.IsEven(4)}");
// Console.WriteLine($"[CONST] IsOddInt(5): {MiscellaneousOperations.IsOddInt(5)}");
// Console.WriteLine($"[CONST] AbsoluteValue(-42): {MiscellaneousOperations.AbsoluteValue(-42)}");
// Console.WriteLine($"[CONST] AbsoluteValueDouble(-3.14): {MiscellaneousOperations.AbsoluteValueDouble(-3.14):F2}");
// Console.WriteLine($"[CONST] Sign(-5.5): {MiscellaneousOperations.Sign(-5.5)}");
// Console.WriteLine($"[CONST] IsInRange(5,1,10): {MiscellaneousOperations.IsInRange(5, 1, 10)}");
// Console.WriteLine($"[CONST] Percentage(25,200): {MiscellaneousOperations.Percentage(25, 200):F2}");
// Console.WriteLine($"[CONST] PercentageOf(10,200): {MiscellaneousOperations.PercentageOf(10, 200):F2}");
// Console.WriteLine($"[CONST] PercentageIncrease(50,75): {MiscellaneousOperations.PercentageIncrease(50, 75):F2}");
// Console.WriteLine($"[CONST] PercentageDecrease(75,50): {MiscellaneousOperations.PercentageDecrease(75, 50):F2}");
// Console.WriteLine($"[CONST] DivideAndRoundUp(10,3): {MiscellaneousOperations.DivideAndRoundUp(10, 3)}");
// Console.WriteLine($"[CONST] IsPowerOfTwo(16): {MiscellaneousOperations.IsPowerOfTwo(16)}");
// // IsPowerOfTwo - mixed
// Console.WriteLine($"[MIXED] IsPowerOfTwo(varInt): {MiscellaneousOperations.IsPowerOfTwo(varInt)}");
// // IsPowerOfTwo - alleen variabelen
// Console.WriteLine($"[VARS ] IsPowerOfTwo(varInt8): {MiscellaneousOperations.IsPowerOfTwo(varInt8)}");
//
// // Percentage - alleen constanten
// Console.WriteLine($"[CONST] Percentage(25,100): {MiscellaneousOperations.Percentage(25, 100):F2}");
// // Percentage - mixed
// Console.WriteLine($"[MIXED] Percentage(varDouble*10,100): {MiscellaneousOperations.Percentage(varDouble * 10, 100):F2}");
// // Percentage - alleen variabelen
// Console.WriteLine($"[VARS ] Percentage(varDouble11,varDouble5): {MiscellaneousOperations.Percentage(varDouble11, varDouble5):F2}");

Console.WriteLine();
