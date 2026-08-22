namespace ConstExpr.Tests.Color;

[InheritsTests]
public class HSVToRGBTest : BaseTestWithRandomValues<Func<double, double, double, (byte, byte, byte)>>
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
			var q = v * (1.0 - s * f);
			var t = v * (1.0 - s * (1.0 - f));

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
			var r = v;
			var g = v;
			var b = v;

			if (s != 0D)
			{
				h = h == 360D ? 0D : h * 0.016666666666666666;

				var i = (int) h;
				var f = h - i;
				var doubleMultiplyAddEstimate = Double.MultiplyAddEstimate(-s, v, v);

				switch (i)
				{
					case 0:
					{
						g = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(-f, s, s), v, v);
						b = doubleMultiplyAddEstimate;

						break;
					}

					case 1:
					{
						r = v * Double.MultiplyAddEstimate(-s, f, 1D);
						b = doubleMultiplyAddEstimate;

						break;
					}

					case 2:
					{
						r = doubleMultiplyAddEstimate;
						b = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(-f, s, s), v, v);

						break;
					}

					case 3:
					{
						r = doubleMultiplyAddEstimate;
						g = v * Double.MultiplyAddEstimate(-s, f, 1D);

						break;
					}

					case 4:
					{
						r = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(-f, s, s), v, v);
						g = doubleMultiplyAddEstimate;

						break;
					}

					default:
					{
						g = doubleMultiplyAddEstimate;
						b = v * Double.MultiplyAddEstimate(-s, f, 1D);

						break;
					}
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}),
		Create((_, s, v) =>
		{
			var r = v;
			var g = v;

			if (s != 0D)
			{
				r = Double.MultiplyAddEstimate(-s, v, v);
				g = v * Double.MultiplyAddEstimate(s, -0.3333333333333335, 1D);
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (v * 255D));
		}, [ 200.0, Unknown, Unknown ]),
		Create((h, _, v) =>
		{
			double r, g, b;

			h = h == 360D ? 0D : h * 0.016666666666666666;

			var i = (int) h;
			var f = h - i;
			var prod = v * 0.5;

			switch (i)
			{
				case 0:
				{
					r = v;
					g = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(f, -0.5, 0.5), v, v);
					b = prod;

					break;
				}

				case 1:
				{
					r = v * Double.MultiplyAddEstimate(f, -0.5, 1D);
					g = v;
					b = prod;

					break;
				}

				case 2:
				{
					r = prod;
					g = v;
					b = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(f, -0.5, 0.5), v, v);

					break;
				}

				case 3:
				{
					r = prod;
					g = v * Double.MultiplyAddEstimate(f, -0.5, 1D);
					b = v;

					break;
				}

				case 4:
				{
					r = Double.MultiplyAddEstimate(-Double.MultiplyAddEstimate(f, -0.5, 0.5), v, v);
					g = prod;
					b = v;

					break;
				}

				default:
				{
					r = v;
					g = prod;
					b = v * Double.MultiplyAddEstimate(f, -0.5, 1D);

					break;
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}, [ Unknown, 0.5, Unknown ]),
		Create((h, s, _) =>
		{
			var r = 0.5;
			var g = 0.5;
			var b = 0.5;

			if (s != 0D)
			{
				h = h == 360D ? 0D : h * 0.016666666666666666;

				var i = (int) h;
				var f = h - i;
				var doubleMultiplyAddEstimate = Double.MultiplyAddEstimate(s, -0.5, 0.5);

				switch (i)
				{
					case 0:
					{
						g = Double.MultiplyAddEstimate(Double.MultiplyAddEstimate(-f, s, s), -0.5, 0.5);
						b = doubleMultiplyAddEstimate;

						break;
					}

					case 1:
					{
						r = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;
						b = doubleMultiplyAddEstimate;

						break;
					}

					case 2:
					{
						r = doubleMultiplyAddEstimate;
						b = Double.MultiplyAddEstimate(Double.MultiplyAddEstimate(-f, s, s), -0.5, 0.5);

						break;
					}

					case 3:
					{
						r = doubleMultiplyAddEstimate;
						g = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;

						break;
					}

					case 4:
					{
						r = Double.MultiplyAddEstimate(Double.MultiplyAddEstimate(-f, s, s), -0.5, 0.5);
						g = doubleMultiplyAddEstimate;

						break;
					}

					default:
					{
						g = doubleMultiplyAddEstimate;
						b = Double.MultiplyAddEstimate(-s, f, 1D) * 0.5;

						break;
					}
				}
			}

			return ((byte) (r * 255D), (byte) (g * 255D), (byte) (b * 255D));
		}, [ Unknown, Unknown, 0.5 ]),
		Create((_, s, _) => ((byte) (s == 0D ? 127.5D : Double.MultiplyAddEstimate(s, -127.5, 127.5)), 127, 127), [ 180, Unknown, 0.5 ])
	];
}