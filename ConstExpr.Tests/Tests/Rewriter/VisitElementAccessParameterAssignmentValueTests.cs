namespace ConstExpr.Tests.Rewriter;

/// <summary>An assignment used as a value is emitted as the write itself; its value is the written literal.</summary>
[InheritsTests]
public class VisitElementAccessParameterAssignmentValueTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		return arr[0] = 5;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);

			return arrRef = 5;
			""", new[] { 1, 2 })
	];
}