namespace ConstExpr.Tests.Rewriter;

/// <summary>x % x = 0 when x != 0.</summary>
[InheritsTests]
public class ModuloIdempotencyTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x % x),
		Create(_ => 0, [ 7 ]),
		Create(_ => 0, [ -3 ]),
	];
}