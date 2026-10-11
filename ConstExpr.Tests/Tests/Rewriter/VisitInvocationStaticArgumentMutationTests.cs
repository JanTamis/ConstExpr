namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   An unconditional static call that mutates its array argument in place (<c>Array.Reverse(data)</c>) must run
///   against the tracked array itself, so later reads fold to the mutated contents.
/// </summary>
[InheritsTests]
public class VisitInvocationStaticArgumentMutationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(data =>
	{
		System.Array.Reverse(data);

		return data[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			System.Array.Reverse(data);

			return 3;
			""", new[] { 1, 2, 3 }),
		Create("""
			ref var dataRef = ref MemoryMarshal.GetArrayDataReference(data);

			System.Array.Reverse(data);

			return dataRef;
			""", Unknown)
	];
}