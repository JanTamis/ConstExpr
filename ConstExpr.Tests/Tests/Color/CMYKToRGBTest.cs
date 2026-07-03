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
			var prod = (1D - k) * 255D;

			return ((byte) ((1D - c) * prod), (byte) ((1D - m) * prod), (byte) ((1D - y) * prod));
		}),
		Create((c, m, y, _) => ((byte) ((1D - c) * 153D), (byte) ((1D - m) * 153D), (byte) ((1D - y) * 153D)), [ Unknown, Unknown, Unknown, 0.4 ])
	];
}