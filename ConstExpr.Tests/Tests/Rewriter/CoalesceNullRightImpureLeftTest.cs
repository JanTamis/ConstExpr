namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests the null-coalescing right-null fold on a non-trivial (impure, unknown-value) left operand:
///   Environment.GetEnvironmentVariable(x) ?? null => Environment.GetEnvironmentVariable(x). x is
///   Unknown, so the call can't be reflectively pre-evaluated - this exercises the strategy on an
///   InvocationExpressionSyntax rather than the bare identifier the other coalesce tests use.
/// </summary>
[InheritsTests]
public class CoalesceNullRightImpureLeftTest : BaseTestWithRandomValues<Func<string, string?>>
{
	public override string TestMethod => GetString(x => Environment.GetEnvironmentVariable(x) ?? null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Environment.GetEnvironmentVariable(x))
	];
}