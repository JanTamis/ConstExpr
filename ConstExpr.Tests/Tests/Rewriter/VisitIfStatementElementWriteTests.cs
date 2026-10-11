namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   An element write (<c>data[0] = 9</c>, <c>data[1]++</c>) inside an if whose condition is unknown must not be
///   folded into the tracked array: it only happens on some paths at runtime, so later reads stay runtime reads.
/// </summary>
[InheritsTests]
public class VisitIfStatementElementWriteTests : BaseTestWithRandomValues<Func<int[], bool, int>>
{
	public override string TestMethod => GetString((data, flag) =>
	{
		if (flag)
		{
			data[0] = 9;
			data[1]++;
		}

		return data[0] + data[1];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var dataRef = ref MemoryMarshal.GetArrayDataReference(data);

			if (flag)
			{
				dataRef = 9;
				Unsafe.Add(ref dataRef, 1)++;
			}

			return dataRef + Unsafe.Add(ref dataRef, 1);
			""", new[] { 1, 2, 3 }, Unknown),
		Create("return 12;", new[] { 1, 2, 3 }, true),
		Create("return 3;", new[] { 1, 2, 3 }, false)
	];
}