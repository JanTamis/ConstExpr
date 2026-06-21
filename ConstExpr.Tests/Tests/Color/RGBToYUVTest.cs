using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToYUVTest() : BaseTest<Func<byte, byte, byte, (double, double, double)>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((r, g, b) =>
	{
		var y = r * .299000 + g * .587000 + b * .114000;
		var u = r * -.168736 + g * -.331264 + b * .500000 + 128;
		var v = r * .500000 + g * -.418688 + b * -.081312 + 128;

		return (y, u, v);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((r, g, b) => (Double.MultiplyAddEstimate(b, 0.114, Double.MultiplyAddEstimate(r, 0.299, g * 0.587)), Double.MultiplyAddEstimate(b, 0.5, -(r * 0.168736 + g * 0.331264)) + 128D, Double.MultiplyAddEstimate(r, 0.5, -(g * 0.418688)) + -(b * 0.081312) + 128D))
	];
}