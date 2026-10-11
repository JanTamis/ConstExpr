namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A mutating instance call on a PARAMETER receiver (<c>list.Add(4)</c>) that gets reflectively executed against
///   the tracked value may fold later reads (<c>list.Count</c> → 4), but the call itself must stay in the output:
///   the parameter is the caller's object (possibly aliased) and has to receive the mutation at runtime.
/// </summary>
[InheritsTests]
public class VisitInvocationParameterReceiverMutationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(list =>
	{
		list.Add(4);

		return list.Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			list.Add(4);

			return 4;
			""", new List<int> { 1, 2, 3 })
	];
}