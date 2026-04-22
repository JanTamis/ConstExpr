using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Color;

[InheritsTests]
public class RGBToHSLTest() : BaseTest<Func<byte, byte, byte, (int, double, double)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((r, g, b) =>
	{
		var h = 0;
		var s = 0.0;
		var l = 0.0;

		var normalizedR = (r / 255.0);
		var normalizedG = (g / 255.0);
		var normalizedB = (b / 255.0);

		var min = System.Math.Min(System.Math.Min(normalizedR, normalizedG), normalizedB);
		var max = System.Math.Max(System.Math.Max(normalizedR, normalizedG), normalizedB);
		var delta = max - min;

		l = (max + min) / 2;

		if (delta == 0)
		{
			h = 0;
			s = 0.0;
		}
		else
		{
			s = (l <= 0.5) ? (delta / (max + min)) : (delta / (2 - max - min));

			double hue = 0.0;

			if (normalizedR == max)
			{
				hue = ((normalizedG - normalizedB) / 6) / delta;
			}
			else if (normalizedG == max)
			{
				hue = (1.0 / 3) + ((normalizedB - normalizedR) / 6) / delta;
			}
			else
			{
				hue = (2.0 / 3) + ((normalizedR - normalizedG) / 6) / delta;
			}

			if (hue < 0)
				hue += 1;
			if (hue > 1)
				hue -= 1;

			h = (int) (hue * 360);
		}

		return (h, s, l);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var h = 0;
			var s = 0D;
			var l = 0D;
			
			var normalizedR = r * 0.00392156862745098;
			var normalizedG = g * 0.00392156862745098;
			var normalizedB = b * 0.00392156862745098;
			
			var min = Double.MinNative(Double.MinNative(normalizedR, normalizedG), normalizedB);
			var max = Double.MaxNative(Double.MaxNative(normalizedR, normalizedG), normalizedB);
			
			var delta = max - min;

			l = (max + min) * 0.5;

			if (delta == 0D)
			{
				h = 0;
				s = 0D;
			}
			else
			{
				s = l <= 0.5 ? delta / (max + min) : delta / (2D - max - min);
				
				var hue = 0.0;

			if (normalizedR == max)
			{
				hue = (normalizedG - normalizedB) * 0.16666666666666666 / delta;
			}
			else
			{
				hue = normalizedG == max ? 0.3333333333333333 + (normalizedB - normalizedR) * 0.16666666666666666 / delta : 0.6666666666666666 + (normalizedR - normalizedG) * 0.16666666666666666 / delta;

				if (hue < 0D)
					hue += 1D;

				if (hue > 1D)
					hue -= 1D;

				h = (int)(hue * 360D);
			}

			return (h, s, l);
			"""),
	];
}