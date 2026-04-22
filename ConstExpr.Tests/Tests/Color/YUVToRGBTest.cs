using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Color;

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
		Create("""
			var r = (byte)Double.MultiplyAddEstimate(1.4075, v - 128D, y);
			var g = (byte)Double.MultiplyAddEstimate(-0.7169, v - 128D, Double.MultiplyAddEstimate(-0.3455, u - 128D, y));
			var b = (byte)Double.MultiplyAddEstimate(1.779, u - 128D, y);
			
			return (r, g, b);
			""", Unknown, Unknown, Unknown),
	];
}