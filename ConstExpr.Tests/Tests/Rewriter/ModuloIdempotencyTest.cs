namespace ConstExpr.Tests.Rewriter;

/// <summary>x % x = 0 when x != 0.</summary>
[InheritsTests]
public class ModuloIdempotencyTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x == 0 ? throw new DivideByZeroException() : 0),
	];
}