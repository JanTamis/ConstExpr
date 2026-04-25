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
		Create(null),
		Create("""
			var r = (byte)((c - 1D) * 153D);
			var g = (byte)((m - 1D) * 153D);
			var b = (byte)((y - 1D) * 153D);

			return (r, g, b);
			""", Unknown, Unknown, Unknown, 0.4),
	];
}