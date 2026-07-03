namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitArgumentList - visit list
/// </summary>
[InheritsTests]
public class VisitArgumentTests : BaseTest<Func<string, string, string, string>>
{
	public override string TestMethod => GetString((a, b, c) => System.String.Concat(a, b, c));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return String.Concat(a, b, c);"),
		Create((_, _, _) => "abc", [ "a", "b", "c" ]),
		Create((_, b, _) => System.String.Concat("a", b, "c"), [ "a", Unknown, "c" ]),
		Create((a, _, c) => System.String.Concat(a, "b", c), [ Unknown, "b", Unknown ]),
		Create((a, _, _) => string.Concat(a, "ab"), [ Unknown, "a", "b" ]),
		Create((a, _, _) => string.Concat(a, "c"), [ Unknown, null, "c" ]),
		Create((_, _, _) => "", [ System.String.Empty, System.String.Empty, System.String.Empty ])
	];
}