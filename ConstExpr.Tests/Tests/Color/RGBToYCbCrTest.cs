using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToYCbCrTest() : BaseTest<Func<byte, byte, byte, (double, double, double)>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
		Create((r, g, b) =>
		{
			var fr = r * 0.00392156862745098;
			var fg = g * 0.00392156862745098;
			var fb = b * 0.00392156862745098;

			return (Double.MultiplyAddEstimate(fb, 0.1145, Double.MultiplyAddEstimate(fr, 0.2989, fg * 0.5866)), Double.MultiplyAddEstimate(fb, 0.5, Double.MultiplyAddEstimate(-fg, 0.3313, -(fr * 0.1687))), Double.MultiplyAddEstimate(-fb, 0.0816, Double.MultiplyAddEstimate(fr, 0.5, -(fg * 0.4184))));
		}),
	];
}