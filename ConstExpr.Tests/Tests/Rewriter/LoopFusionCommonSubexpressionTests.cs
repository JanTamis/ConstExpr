using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A common expression in two separate loops cannot be eliminated before the loops are fused:
///   each loop body contains it once. The post-loop fixed-point phase sees the merged body and
///   introduces one local for both uses.
/// </summary>
[InheritsTests]
public class LoopFusionCommonSubexpressionTests() : BaseTestWithRandomValues<Func<int, int, int, int>>(
	optimizations: OptimizationFlags.LoopFusion | OptimizationFlags.CommonSubexpressionElimination)
{

	protected override int MaxRandomMagnitudeBits => 5;
	public override string TestMethod => GetString((n, x, y) =>
	{
		var sum = 0;
		var total = 0;

		for (var i = 0; i < n; i++)
		{
			sum += x * y;
		}

		for (var i = 0; i < n; i++)
		{
			total += x * y;
		}

		return sum + total;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, x, y) =>
		{
			var sum = 0;
			var total = 0;

			for (var i = 0; i < n; i++)
			{
				var prod = x * y;
				sum += prod;
				total += prod;
			}

			return sum + total;
		}, [ Unknown, Unknown, Unknown ])
	];
}