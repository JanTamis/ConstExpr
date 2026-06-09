using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Trim().Trim() → s.Trim(): idempotency.</summary>
[InheritsTests]
public class StringTrimIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => s.Trim().Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.Trim()),
	];
}