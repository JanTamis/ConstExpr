namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitMemberAccessExpression - field/property evaluation
/// </summary>
[InheritsTests]
public class VisitMemberAccessExpressionTests : BaseTest<Func<string, bool, (int, int, string, bool)>>
{
	public override string TestMethod => GetString((s, useEmpty) =>
	{
		var target = useEmpty ? System.String.Empty : s;
		var len = target.Length;
		var helloLen = "hello".Length;
		var empty = System.String.Empty;
		var isEmpty = target == System.String.Empty;

		return (len, helloLen, empty, isEmpty);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, useEmpty) =>
		{
			var target = useEmpty ? System.String.Empty : s;

			return (target.Length, 5, System.String.Empty, target == System.String.Empty);
		}),
		Create((_, _) => (5, 5, System.String.Empty, false), [ "hello", false ]),
		Create((_, _) => (0, 5, System.String.Empty, true), [ "ignored", true ]),
		Create((_, _) => (3, 5, System.String.Empty, false), [ "cat", false ]),
		Create((_, _) => (0, 5, System.String.Empty, true), [ System.String.Empty, true ])
	];
}