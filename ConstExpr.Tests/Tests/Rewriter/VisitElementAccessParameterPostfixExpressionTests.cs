namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A postfix increment inside a larger expression keeps its <c>++</c> form, so the expression still
///   observes the previous value; the later read folds to the incremented one.
/// </summary>
[InheritsTests]
public class VisitElementAccessParameterPostfixExpressionTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		var x = arr[1]++;

		return x + arr[1];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);

			return Unsafe.Add(ref arrRef, 1)++ + 3;
			""", new[] { 1, 2 })
	];
}