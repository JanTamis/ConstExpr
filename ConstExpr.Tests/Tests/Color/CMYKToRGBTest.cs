using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class CMYKToRGBTest() : BaseTest<Func<double, double, double, double, (byte, byte, byte)>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
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
			var diff = 1D - k;

			return ((byte) (Double.MultiplyAddEstimate(-c, 255D, 255D) * diff), (byte) (Double.MultiplyAddEstimate(-m, 255D, 255D) * diff), (byte) (Double.MultiplyAddEstimate(-y, 255D, 255D) * diff));
		}),
		Create((c, m, y, _) => ((byte) (Double.MultiplyAddEstimate(-c, 255D, 255D) * 0.6), (byte) (Double.MultiplyAddEstimate(-m, 255D, 255D) * 0.6), (byte) (Double.MultiplyAddEstimate(-y, 255D, 255D) * 0.6)), [ Unknown, Unknown, Unknown, 0.4 ])
	];
}