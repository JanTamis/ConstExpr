using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

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
		Create((h, s, v) =>
		{
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

				var i = (int) Double.Truncate(h);
				var f = h - i;

				switch (i)
				{
					case 0:
					{
						r = v;
						g = v * Double.MultiplyAddEstimate(-s, 1D - f, 1D);
						b = v * (1D - s);

						break;
					}

					case 1:
					{
						r = v * Double.MultiplyAddEstimate(-s, f, 1D);
						g = v;
						b = v * (1D - s);

						break;
					}

					case 2:
					{
						r = v * (1D - s);
						g = v;
						b = v * Double.MultiplyAddEstimate(-s, 1D - f, 1D);

						break;
					}

					case 3:
					{
						r = v * (1D - s);
						g = v * Double.MultiplyAddEstimate(-s, f, 1D);
						b = v;

						break;
					}

					case 4:
					{
						r = v * Double.MultiplyAddEstimate(-s, 1D - f, 1D);
						g = v * (1D - s);
						b = v;

						break;
					}

					default:
					{
						r = v;
						g = v * (1D - s);
						b = v * Double.MultiplyAddEstimate(-s, f, 1D);

						break;
					}
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}),
		Create((_, s, v) =>
		{
			double r = 0D, g = 0D, b = 0D;

			if (s == 0D)
			{
				r = v;
				g = v;
				b = v;
			}
			else
			{
				r = v * (1D - s);
				g = v * Double.MultiplyAddEstimate(-s, 0.3333333333333335, 1D);
				b = v;
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}, [ 200.0, Unknown, Unknown ]),
		Create((h, _, v) =>
		{
			double r = 0D, g = 0D, b = 0D;

			h = h == 360D ? 0D : h * 0.016666666666666666;

			var i = (int) Double.Truncate(h);
			var f = h - i;

			switch (i)
			{
				case 0:
				{
					r = v;
					g = v * Double.MultiplyAddEstimate(-(1D - f), 0.5, 1D);
					b = v * 0.5;

					break;
				}
				case 1:
				{
					r = v * Double.MultiplyAddEstimate(-f, 0.5, 1D);
					g = v;
					b = v * 0.5;

					break;
				}
				case 2:
				{
					r = v * 0.5;
					g = v;
					b = v * Double.MultiplyAddEstimate(-(1D - f), 0.5, 1D);

					break;
				}
				case 3:
				{
					r = v * 0.5;
					g = v * Double.MultiplyAddEstimate(-f, 0.5, 1D);
					b = v;

					break;
				}
				case 4:
				{
					r = v * Double.MultiplyAddEstimate(-(1D - f), 0.5, 1D);
					g = v * 0.5;
					b = v;

					break;
				}
				default:
				{
					r = v;
					g = v * 0.5;
					b = v * Double.MultiplyAddEstimate(-f, 0.5, 1D);

					break;
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}, [ Unknown, 0.5, Unknown ]),
		Create((h, s, _) =>
		{
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

				var i = (int) Double.Truncate(h);
				var f = h - i;

				switch (i)
				{
					case 0:
					{
						r = 0.5;
						g = Double.MultiplyAddEstimate(-s, 1D - f, 1D) * 0.5;
						b = (1D - s) * 0.5;

						break;
					}
					case 1:
					{
						r = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;
						g = 0.5;
						b = (1D - s) * 0.5;

						break;
					}
					case 2:
					{
						r = (1D - s) * 0.5;
						g = 0.5;
						b = Double.MultiplyAddEstimate(-s, 1D - f, 1D) * 0.5;

						break;
					}
					case 3:
					{
						r = (1D - s) * 0.5;
						g = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;
						b = 0.5;

						break;
					}
					case 4:
					{
						r = Double.MultiplyAddEstimate(-s, 1D - f, 1D) * 0.5;
						g = (1D - s) * 0.5;
						b = 0.5;

						break;
					}
					default:
					{
						r = 0.5;
						g = (1D - s) * 0.5;
						b = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;

						break;
					}
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}, [ Unknown, Unknown, 0.5 ])
	];
}