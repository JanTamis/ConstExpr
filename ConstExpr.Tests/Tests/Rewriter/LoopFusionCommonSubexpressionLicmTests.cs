using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The local CSE introduces for a subexpression only duplicated by loop fusion (see
///   <see cref="LoopFusionCommonSubexpressionTests" />) is itself a fresh direct child of the fused
///   loop's block — a shape the earlier LICM pass never saw, since it ran before this declaration
///   existed. With LICM also enabled, that local should be hoisted out of the loop entirely.
/// </summary>
[InheritsTests]
public class LoopFusionCommonSubexpressionLicmTests() : BaseTestWithRandomValues<Func<int, int, int, int>>(
	optimizations: OptimizationFlags.LoopFusion | OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.LoopInvariantCodeMotion)
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
			var prod = x * y;

			for (var i = 0; i < n; i++)
			{
				sum += prod;
				total += prod;
			}

			return sum + total;
		}, [ Unknown, Unknown, Unknown ])
	];
}