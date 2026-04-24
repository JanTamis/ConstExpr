using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Color;

[InheritsTests]
public class RGBToCMYKTest() : BaseTest<Func<byte, byte, byte, (double, double, double, double)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((r, g, b) =>
	{
		var dr = (double) r / 255;
		var dg = (double) g / 255;
		var db = (double) b / 255;
		var k = 1 - System.Math.Max(System.Math.Max(dr, dg), db);

		var c = (1 - dr - k) / (1 - k);
		var m = (1 - dg - k) / (1 - k);
		var y = (1 - db - k) / (1 - k);

		return (c, m, y, k);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var dr = (double)r * 0.00392156862745098;
			var dg = (double)g * 0.00392156862745098;
			var db = (double)b * 0.00392156862745098;
			var k = 1D - Double.MaxNative(Double.MaxNative(dr, dg), db);
			
			var c = (1D - dr - k) / (1D - k);
			var m = (1D - dg - k) / (1D - k);
			var y = (1D - db - k) / (1D - k);

			return (c, m, y, k);
			"""),
	];
}