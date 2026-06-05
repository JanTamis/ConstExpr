using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>
///   a*x*x + b*x + c is rewritten into Horner form (a*x + b)*x + c under HornerPolynomial.
/// </summary>
[InheritsTests]
public class HornerPolynomialTest() : BaseTest<Func<double, double, double, double, double>>(FastMathFlags.HornerPolynomial)
{
	public override string TestMethod => GetString((a, b, c, x) => a * x * x + b * x + c);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (a * x + b) * x + c;", Unknown, Unknown, Unknown, Unknown)
	];
}

/// <summary>
///   With FusedMultiplyAdd enabled too, the nested Horner multiplies contract into nested FMA calls.
/// </summary>
[InheritsTests]
public class HornerPolynomialFmaTest() : BaseTest<Func<double, double, double, double, double>>(FastMathFlags.HornerPolynomial | FastMathFlags.FusedMultiplyAdd)
{
	public override string TestMethod => GetString((a, b, c, x) => a * x * x + b * x + c);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.FusedMultiplyAdd(Double.FusedMultiplyAdd(a, x, b), x, c);", Unknown, Unknown, Unknown, Unknown)
	];
}

/// <summary>
///   Conservatism guard: x*x + y*y is a polynomial in two variables (both degree 2), so it is
///   ambiguous and must be left untouched.
/// </summary>
[InheritsTests]
public class HornerPolynomialMultiVariableTest() : BaseTest<Func<double, double, double>>(FastMathFlags.HornerPolynomial)
{
	public override string TestMethod => GetString((x, y) => x * x + y * y);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown)
	];
}

/// <summary>
///   Conservatism guard: a degree-1 expression a*x + b is not a Horner candidate (needs degree ≥ 2)
///   and must be left untouched.
/// </summary>
[InheritsTests]
public class HornerPolynomialDegreeOneTest() : BaseTest<Func<double, double, double, double>>(FastMathFlags.HornerPolynomial)
{
	public override string TestMethod => GetString((a, b, x) => a * x + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown, Unknown)
	];
}