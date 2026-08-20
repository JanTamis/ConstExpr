namespace ConstExpr.Tests.Color;

[InheritsTests]
public class CMYKToRGBTest : BaseTestWithRandomValues<Func<double, double, double, double, (byte, byte, byte)>>
{
	public override string TestMethod => GetString((c, m, y, k) =>
	{
		var r = (byte) (255 * (1 - c) * (1 - k));
		var g = (byte) (255 * (1 - m) * (1 - k));
		var b = (byte) (255 * (1 - y) * (1 - k));

		return (r, g, b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((c, m, y, k) =>
		{
			var prod = (1D - k) * 255D;

			return ((byte) Double.MultiplyAddEstimate(prod, -c, prod), (byte) Double.MultiplyAddEstimate(prod, -m, prod), (byte) Double.MultiplyAddEstimate(prod, -y, prod));
		}),
		Create((c, m, y, _) => ((byte) Double.MultiplyAddEstimate(c, -153D, 153D), (byte) Double.MultiplyAddEstimate(m, -153D, 153D), (byte) Double.MultiplyAddEstimate(y, -153D, 153D)), [ Unknown, Unknown, Unknown, 0.4 ])
	];
}