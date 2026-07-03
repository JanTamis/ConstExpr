using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.TrimEnd().TrimEnd() → s.TrimEnd(): idempotency.</summary>
[InheritsTests]
public class StringTrimEndIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(s => s.TrimEnd().TrimEnd());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.TrimEnd())
	];
}