namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A mutating call inside a section of a switch whose governing value is unknown must not leak into the tracked
///   receiver, so the final <c>ToArray()</c> cannot fold to a literal.
/// </summary>
[InheritsTests]
public class VisitSwitchStatementMutatingReceiverTests : BaseTestWithRandomValues<Func<int, int[]>>
{
	public override string TestMethod => GetString(mode =>
	{
		var result = new List<int>();

		switch (mode)
		{
			case 0:
				result.Add(10);
				break;
			case 1:
				result.Add(20);
				break;
		}

		return result.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var result = new List<int>();

			switch (mode)
			{
				case 0:
				{
					result.Add(10);
					break;
				}
				case 1:
				{
					result.Add(20);
					break;
				}
			}

			return result.ToArray();
			""", Unknown),
		Create("return [20];", 1)
	];
}