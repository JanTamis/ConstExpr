namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A mutating call inside an unknown-condition if within an unrolled foreach must not leak into the tracked
///   receiver, so the final <c>ToArray()</c> cannot fold to a literal.
/// </summary>
[InheritsTests]
public class VisitForEachStatementMutatingReceiverTests : BaseTestWithRandomValues<Func<int[], int, int[]>>
{
	public override string TestMethod => GetString((data, threshold) =>
	{
		var result = new List<int>();

		foreach (var item in data)
		{
			if (item > threshold)
			{
				result.Add(item);
			}
		}

		return result.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var result = new List<int>();

			if (threshold < 1)
				result.Add(1);

			if (threshold < 2)
				result.Add(2);

			if (threshold < 3)
				result.Add(3);

			return result.ToArray();
			""", new[] { 1, 2, 3 }, Unknown),
		Create("return [3];", new[] { 1, 2, 3 }, 2)
	];
}