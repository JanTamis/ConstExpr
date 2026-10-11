namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A static call that mutates its array argument in place (<c>Array.Reverse(data)</c>) inside an if whose
///   condition is unknown must not be folded into the tracked array, and must keep the argument's identity
///   rather than inlining it to a throwaway collection literal.
/// </summary>
[InheritsTests]
public class VisitIfStatementStaticArgumentMutationTests : BaseTest<Func<int[], bool, int>>
{
	public override string TestMethod => GetString((data, flag) =>
	{
		if (flag)
		{
			System.Array.Reverse(data);
		}

		return data[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var dataRef = ref MemoryMarshal.GetArrayDataReference(data);

			if (flag)
				System.Array.Reverse(data);

			return dataRef;
			""", new[] { 1, 2, 3 }, Unknown),
		Create("""
			System.Array.Reverse(data);

			return 3;
			""", new[] { 1, 2, 3 }, true),
		Create("return 1;", new[] { 1, 2, 3 }, false)
	];
}