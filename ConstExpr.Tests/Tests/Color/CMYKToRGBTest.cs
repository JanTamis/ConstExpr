using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class CMYKToRGBTest() : BaseTest<Func<double, double, double, double, (byte, byte, byte)>>(FastMathFlags.FastMath)
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
		Create("return ((byte)(255D * (1D - c) * (1D - k)), (byte)(255D * (1D - m) * (1D - k)), (byte)(255D * (1D - y) * (1D - k)));"),
		Create("return ((byte)((1D - c) * 153D), (byte)((1D - m) * 153D), (byte)((1D - y) * 153D));", Unknown, Unknown, Unknown, 0.4),
	];
}