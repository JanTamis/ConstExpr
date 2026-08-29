using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>
///   Regression guard for the dispatch reorder (<c>else if (NoNaN)</c> → <c>else</c>): a
///   generic-math call (<c>int.Clamp</c>, resolved off <c>Int32</c> rather than <c>System.Math</c>)
///   under <see cref="FastMathFlags.Strict" /> now reaches <c>TryOptimizeMathMethod</c> where it
///   previously never did. <c>ClampFunctionOptimizer</c> keeps the default
///   <c>RequiredFlags = [NoNaN]</c> (its branchless-ternary fallback double-evaluates and diverges on
///   a signed-zero / NaN edge), so it is filtered out and the call is left exactly as written — the
///   reorder must not leak a rewrite through the generic-math branch.
/// </summary>
[InheritsTests]
public class GenericMathClampStrictNotOptimizedTest() : BaseTest<Func<int, int, int, int>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((x, lo, hi) => Int32.Clamp(x, lo, hi));

	// CreateDefault() asserts the pipeline leaves the body byte-identical to the source — robust to
	// whether the call renders as int.Clamp or Int32.Clamp.
	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}