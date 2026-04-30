using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class YCbCrToRGBTest() : BaseTest<Func<double, double, double, (byte, byte, byte)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((y, cb, cr) =>
	{
		var r = System.Math.Max(0.0f, System.Math.Min(1.0f, y + 0.0000 * cb + 1.4022 * cr));
		var g = System.Math.Max(0.0f, System.Math.Min(1.0f, y - 0.3456 * cb - 0.7145 * cr));
		var b = System.Math.Max(0.0f, System.Math.Min(1.0f, y + 1.7710 * cb + 0.0000 * cr));

		return ((byte) (r * 255), (byte) (g * 255), (byte) (b * 255));
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ((byte)(Double.ClampNative(Double.MultiplyAddEstimate(cr, 1.4022, y), 0D, 1D) * 255D), (byte)(Double.ClampNative(Double.MultiplyAddEstimate(-cr, 0.7145, Double.MultiplyAddEstimate(-cb, 0.3456, y)), 0D, 1D) * 255D), (byte)(Double.ClampNative(Double.MultiplyAddEstimate(cb, 1.771, y), 0D, 1D) * 255D));"),
	];
}