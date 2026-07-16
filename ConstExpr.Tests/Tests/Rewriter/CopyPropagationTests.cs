using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for Copy Propagation (CP).
///   A local that is a plain copy of another variable (<c>var y = x;</c>) has its reads replaced
///   by the source variable, after which the dead copy declaration is pruned. The copy is read
///   more than once (in a loop and after it) — single-use copies are already inlined by the
///   always-on partial rewriter, so multi-use is the shape this pass exists for.
/// </summary>
[InheritsTests]
public class CopyPropagationTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.CopyPropagation)
{
	public override string TestMethod => GetString((n, x) =>
	{
		var y = x;
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += y;
		}

		return sum + y;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: both reads of the copy become reads of the source; the dead copy is pruned.
		Create((n, x) =>
		{
			var sum = 0;

			for (var i = 0; i < n; i++)
			{
				sum += x;
			}

			return sum + x;
		}, [ Unknown, Unknown ]),

		// Known inputs fold completely through the interpreter: 3 iterations of +4, plus 4 = 16.
		// This anchors the semantics of the shape independently of the rewriter.
		Create((_, _) => 16, [ 3, 4 ])
	];
}