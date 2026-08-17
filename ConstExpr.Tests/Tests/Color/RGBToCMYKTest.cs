namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToCMYKTest : BaseTestWithRandomValues<Func<byte, byte, byte, (double, double, double, double)>>
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
		Create((r, g, b) =>
		{
			var max = Double.MaxNative(Double.MaxNative(r, g), b);
			var k = 1D - max * 0.00392156862745098;
			var invMax = Double.ReciprocalEstimate(max);

			return ((max - r) * invMax, (max - g) * invMax, (max - b) * invMax, k);
		})
	];
}