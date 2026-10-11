namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A mutating call (<c>List&lt;T&gt;.Add</c>) inside an if whose condition is unknown must not be folded into the
///   tracked receiver: the call only runs on some paths at runtime, so the receiver's value after the if is unknown.
/// </summary>
[InheritsTests]
public class VisitIfStatementMutatingReceiverTests : BaseTestWithRandomValues<Func<double[], double, int[]>>
{
	public override string TestMethod => GetString((data, threshold) =>
	{
		var outliers = new List<int>();

		for (var i = 0; i < data.Length; i++)
		{
			if (data[i] > threshold)
			{
				outliers.Add(i);
			}
		}

		return outliers.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var outliers = new List<int>();

			if (threshold < 1D)
				outliers.Add(0);

			if (threshold < 2D)
				outliers.Add(1);

			if (threshold < 3D)
				outliers.Add(2);

			return outliers.ToArray();
			""", new[] { 1D, 2D, 3D }, Unknown),
		Create("return [2];", new[] { 1D, 2D, 3D }, 2.5D)
	];
}