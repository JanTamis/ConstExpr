using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class CMYKToRGBTest() : BaseTest<Func<double, double, double, double, (byte, byte, byte)>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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

			return ((byte) ((1D - c) * 255D * diff), (byte) ((1D - m) * 255D * diff), (byte) ((1D - y) * 255D * diff));
		}),
		Create((c, m, y, _) => ((byte) ((1D - c) * 153D), (byte) ((1D - m) * 153D), (byte) ((1D - y) * 153D)), [ Unknown, Unknown, Unknown, 0.4 ]),
	];
}