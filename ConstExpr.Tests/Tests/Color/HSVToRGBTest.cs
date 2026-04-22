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
				h = h == 360D ? 0D : h * 0.016666666666666666;

				var i = (int)Double.Truncate(h);
				var f = h - i;
				var p = v * (1D - s);
				var q = v * Double.MultiplyAddEstimate(-s, f, 1D);
				var t = v * Double.MultiplyAddEstimate(-s, f - 1D, 1D);

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
				var p = v * (1D - s);
				var q = v * Double.MultiplyAddEstimate(-s, 0.3333333333333335, 1D);
				var t = v * Double.MultiplyAddEstimate(-s, 0.6666666666666665, 1D);

				r = p;
				g = q;
				b = v;
			}

			return ((byte)(r * 255D), (byte)(g * 255D), (byte)(b * 255D));
			""", 200.0, Unknown, Unknown),
		Create("""
			double r = 0D, g = 0D, b = 0D;

			h = h == 360D ? 0D : h * 0.016666666666666666;

			var i = (int)Double.Truncate(h);
			var f = h - i;
			var p = v * 0.5;
			var q = v * Double.MultiplyAddEstimate(-0.5, f, 1D);
			var t = v * Double.MultiplyAddEstimate(-0.5, f - 1D, 1D);

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

			return ((byte)(r * 255D), (byte)(g * 255D), (byte)(b * 255D));
			""", Unknown, 0.5, Unknown),
		Create("""
		double r = 0D, g = 0D, b = 0D;
		
		if (s == 0D)
		{
			r = 0.5;
			g = 0.5;
			b = 0.5;
		}
		else
		{
			h = h == 360D ? 0D : h * 0.016666666666666666;
		
			var i = (int)Double.Truncate(h);
			var f = h - i;
			var p = 0.5 * (1D - s);
			var q = 0.5 * Double.MultiplyAddEstimate(-s, f, 1D);
			var t = 0.5 * Double.MultiplyAddEstimate(-s, f - 1D, 1D);
		
			switch (i)
			{
				case 0:
				{
					r = 0.5;
					g = t;
					b = p;
		
					break;
				}
				case 1:
				{
					r = q;
					g = 0.5;
					b = p;
		
					break;
				}
				case 2:
				{
					r = p;
					g = 0.5;
					b = t;
		
					break;
				}
				case 3:
				{
					r = p;
					g = q;
					b = 0.5;
		
					break;
				}
				case 4:
				{
					r = t;
					g = p;
					b = 0.5;
		
					break;
				}
				default:
				{
					r = 0.5;
					g = p;
					b = q;
		
					break;
				}
			}
		}
		
		return ((byte)(r * 255D), (byte)(g * 255D), (byte)(b * 255D));
		""", Unknown, Unknown, 0.5)
	];
}