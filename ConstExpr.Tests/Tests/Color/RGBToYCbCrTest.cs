using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Color;

[InheritsTests]
public class RGBToYCbCrTest() : BaseTest<Func<byte, byte, byte, (double, double, double)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((r, g, b) =>
	{
		var fr = r / 255d;
		var fg = g / 255d;
		var fb = b / 255d;

		var y = 0.2989 * fr + 0.5866 * fg + 0.1145 * fb;
		var cb = -0.1687 * fr - 0.3313 * fg + 0.5000 * fb;
		var cr = 0.5000 * fr - 0.4184 * fg - 0.0816 * fb;

		return (y, cb, cr);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var fr = r * 0.00392156862745098;
			var fg = g * 0.00392156862745098;
			var fb = b * 0.00392156862745098;
			var y = Double.MultiplyAddEstimate(0.1145, fb, Double.MultiplyAddEstimate(0.2989, fr, fg * 0.5866));
			var cb = Double.MultiplyAddEstimate(0.5, fb, Double.MultiplyAddEstimate(-0.3313, fg, -(fr * 0.1687)));
			var cr = Double.MultiplyAddEstimate(-0.0816, fb, Double.MultiplyAddEstimate(0.5, fr, -(fg * 0.4184)));

			return (y, cb, cr);
			"""),
	];
}