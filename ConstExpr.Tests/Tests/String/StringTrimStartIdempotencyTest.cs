using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.TrimStart().TrimStart() → s.TrimStart(): idempotency.</summary>
[InheritsTests]
public class StringTrimStartIdempotencyTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => s.TrimStart().TrimStart());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.TrimStart()),
	];
}