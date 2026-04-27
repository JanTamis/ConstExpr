using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

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
			var y = Double.MultiplyAddEstimate(b, 0.00044901960784313725, Double.MultiplyAddEstimate(r, 0.001172156862745098, g * 0.002300392156862745));
			var cb = Double.MultiplyAddEstimate(b, 0.00196078431372549, Double.MultiplyAddEstimate(-g, 0.0012992156862745097, -(r * 0.00392156862745098 * 0.1687)));
			var cr = Double.MultiplyAddEstimate(-b, 0.00032, Double.MultiplyAddEstimate(r, 0.00196078431372549, -(g * 0.0016407843137254902)));
			
			return (y, cb, cr);
			"""),
	];
}