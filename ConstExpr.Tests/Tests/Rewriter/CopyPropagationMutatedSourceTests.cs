using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative test for Copy Propagation: the source variable is written inside the loop, so the
///   copy and the source genuinely diverge at runtime. The copy is deliberately multi-use — the
///   always-on partial rewriter leaves multi-use copies alone, so this exercises this pass's own
///   refusal. The body must come out unchanged.
/// </summary>
[InheritsTests]
public class CopyPropagationMutatedSourceTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.CopyPropagation)
{
	public override string TestMethod => GetString((n, x) =>
	{
		var y = x;
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += y;
			x = x + 1;
		}

		return sum + y;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}