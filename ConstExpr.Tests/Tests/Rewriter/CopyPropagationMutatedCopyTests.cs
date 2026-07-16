using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative test for Copy Propagation: the copy itself is reassigned after its declaration, so
///   it is not a pure alias of the source. The pass must refuse and leave the body unchanged.
/// </summary>
[InheritsTests]
public class CopyPropagationMutatedCopyTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.CopyPropagation)
{
	public override string TestMethod => GetString((n, x) =>
	{
		var y = x;
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += y;
		}

		y = y + 1;

		return sum + y;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}