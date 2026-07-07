using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class HSLToRGBTest() : BaseTest<Func<float, float, float, (byte, byte, byte)>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((h, s, l) =>
	{
		byte r = 0;
		byte g = 0;
		byte b = 0;

		if (s == 0)
		{
			r = g = b = (byte) (l * 255);
		}
		else
		{
			float v1, v2;
			var hue = h / 360;

			v2 = l < 0.5 ? l * (1 + s) : l + s - l * s;
			v1 = 2 * l - v2;

			r = (byte) (255 * HueToRGB(v1, v2, hue + 1.0f / 3));
			g = (byte) (255 * HueToRGB(v1, v2, hue));
			b = (byte) (255 * HueToRGB(v1, v2, hue - 1.0f / 3));
		}

		return (r, g, b);

		static float HueToRGB(float v1, float v2, float vH)
		{
			if (vH < 0)
				vH += 1;

			if (vH > 1)
				vH -= 1;

			if (6 * vH < 1)
				return v1 + (v2 - v1) * 6 * vH;

			if (2 * vH < 1)
				return v2;

			if (3 * vH < 2)
				return v1 + (v2 - v1) * (2.0f / 3 - vH) * 6;

			return v1;
		}
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var r = (byte)0;
			var g = (byte)0;
			var b = (byte)0;

			if (s == 0F)
			{
				r = g = b = (byte)(l * 255F);
			}
			else
			{
				var hue = h * 0.0027777778F;
				var v2 = l < 0.5 ? l * (s + 1F) : Single.MultiplyAddEstimate(-l, s, l + s);
				var v1 = (l + l) - v2;

				r = (byte)(HueToRGB(v1, v2, hue + 0.33333334F) * 255F);
				g = (byte)(HueToRGB(v1, v2, hue) * 255F);
				b = (byte)(HueToRGB(v1, v2, hue - 0.33333334F) * 255F);
			}

			return (r, g, b);
			""")
	];
}