using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Trim().Trim() → s.Trim(): idempotency.</summary>
[InheritsTests]
public class StringTrimIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.Trim().Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s.Trim();"),
	];
}

/// <summary>s.TrimStart().TrimStart() → s.TrimStart(): idempotency.</summary>
[InheritsTests]
public class StringTrimStartIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.TrimStart().TrimStart());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s.TrimStart();"),
	];
}

/// <summary>s.TrimEnd().TrimEnd() → s.TrimEnd(): idempotency.</summary>
[InheritsTests]
public class StringTrimEndIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.TrimEnd().TrimEnd());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s.TrimEnd();"),
	];
}
