namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A static call that mutates its array argument in place but cannot be executed at compile time (here: a runtime
///   comparison) must demote the array to runtime, so later reads are not folded against its stale contents.
/// </summary>
[InheritsTests]
public class VisitInvocationUnexecutedArgumentMutationTests : BaseTest<Func<int[], Comparison<int>, int>>
{
	public override string TestMethod => GetString((data, comparison) =>
	{
		System.Array.Sort(data, comparison);

		return data[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var dataRef = ref MemoryMarshal.GetArrayDataReference(data);

			System.Array.Sort(data, comparison);

			return dataRef;
			""", new[] { 3, 1, 2 }, Unknown)
	];
}