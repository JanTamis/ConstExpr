using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToCMYKTest() : BaseTest<Func<byte, byte, byte, (double, double, double, double)>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
			var dr = r * 0.00392156862745098;
			var dg = g * 0.00392156862745098;
			var db = b * 0.00392156862745098;
			var k = 1D - Double.MaxNative(Double.MaxNative(dr, dg), db);
			var diff = 1D - k;

			return ((1D - dr - k) / diff, (1D - dg - k) / diff, (1D - db - k) / diff, k);
		}),
	];
}