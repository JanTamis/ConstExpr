namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Ensures identifier receivers are not inlined into invalid collection expressions.
/// </summary>
[InheritsTests]
public class VisitIdentifierNameReceiverContextTests : BaseTest<Func<double[], int[]>>
{
	public override string TestMethod => GetString(data =>
	{
		var outliers = new List<int>();

		for (var i = 0; i < data.Length; i++)
		{
			if (data[i] > 0)
			{
				outliers.Add(i);
			}
		}

		return outliers.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(data =>
		{
			var outliers = new List<int>();

			for (var i = 0; i < data.Length; i++)
			{
				if (data[i] > 0D)
					outliers.Add(i);
			}

			return outliers.ToArray();
		}, [ Unknown ])
	];
}