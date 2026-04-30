using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class YUVToRGBTest() : BaseTest<Func<double, double, double, (byte, byte, byte)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((y, u, v) =>
	{
		var r = (byte) (y + 1.4075 * (v - 128));
		var g = (byte) (y - 0.3455 * (u - 128) - (0.7169 * (v - 128)));
		var b = (byte) (y + 1.7790 * (u - 128));

		return (r, g, b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ((byte)Double.MultiplyAddEstimate(v - 128D, 1.4075, y), (byte)Double.MultiplyAddEstimate(-(v - 128D), 0.7169, Double.MultiplyAddEstimate(-(u - 128D), 0.3455, y)), (byte)Double.MultiplyAddEstimate(u - 128D, 1.779, y));"),
	];
}