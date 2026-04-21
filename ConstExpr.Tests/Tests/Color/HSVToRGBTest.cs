using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Color;

[InheritsTests]
public class HSVToRGBTest() : BaseTest<Func<double, double, double, (byte, byte, byte)>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((h, s, v) =>
	{
		double r = 0, g = 0, b = 0;

		if (s == 0)
		{
			r = v;
			g = v;
			b = v;
		}
		else
		{
			if (h == 360)
			{
				h = 0;
			}
			else
			{
				h = h / 60;
			}

			var i = (int) System.Math.Truncate(h);
			var f = h - i;

			var p = v * (1.0 - s);
			var q = v * (1.0 - (s * f));
			var t = v * (1.0 - (s * (1.0 - f)));

			switch (i)
			{
				case 0:
					r = v;
					g = t;
					b = p;
					break;

				case 1:
					r = q;
					g = v;
					b = p;
					break;

				case 2:
					r = p;
					g = v;
					b = t;
					break;

				case 3:
					r = p;
					g = q;
					b = v;
					break;

				case 4:
					r = t;
					g = p;
					b = v;
					break;

				default:
					r = v;
					g = p;
					b = q;
					break;
			}
		}

		return ((byte) (r * 255), (byte) (g * 255), (byte) (b * 255));
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			double r = 0D, g = 0D, b = 0D;
			
			if (s == 0D)
			{
				r = v;
				g = v;
				b = v;
			}
			else
			{
				if (h == 360D)
				{
					h = 0D;
				}
				else
				{
					h *= 0.016666666666666666;
				}
			
				var i = (int)Double.Truncate(h);
				var f = h - i;
			
				var p = v * (1D - s);
				var q = v * (1D - s * f);
				var t = v * (1D - s * (1D - f));
			
				switch (i)
				{
					case 0:
					{
						r = v;
						g = t;
						b = p;
			
						break;
					}
					case 1:
					{
						r = q;
						g = v;
						b = p;
			
						break;
					}
					case 2:
					{
						r = p;
						g = v;
						b = t;
			
						break;
					}
					case 3:
					{
						r = p;
						g = q;
						b = v;
			
						break;
					}
					case 4:
					{
						r = t;
						g = p;
						b = v;
			
						break;
					}
					default:
					{
						r = v;
						g = p;
						b = q;
			
						break;
					}
				}
			}
			
			return ((byte)(r * 255D), (byte)(g * 255D), (byte)(b * 255D));
			""", Unknown, Unknown, Unknown),
	];
}