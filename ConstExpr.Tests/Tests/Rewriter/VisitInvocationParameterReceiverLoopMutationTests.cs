namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same as <see cref="VisitInvocationParameterReceiverMutationTests" />, but for several reflectively executed
///   calls from an unrolled loop: every one must survive, in order.
/// </summary>
[InheritsTests]
public class VisitInvocationParameterReceiverLoopMutationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(list =>
	{
		for (var i = 0; i < 3; i++)
		{
			list.Add(i * 10);
		}

		return list.Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			list.Add(0);
			list.Add(10);
			list.Add(20);

			return 4;
			""", new List<int> { 1 })
	];
}