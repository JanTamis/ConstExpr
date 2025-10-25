using ConstExpr.Core.Attributes;
using System;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class ColorOperations
{
	public static (byte r, byte g, byte b) HslToRgb(float h, float s, float l)
	{
		h %= 360f;

		if (h < 0)
		{
			h += 360f;
		}

		s = Clamp01(s);
		l = Clamp01(l);

		if (s == 0f)
		{
			var vGray = (byte)MathF.Round(l * 255f);
			return (vGray, vGray, vGray);
		}

		var c = (1 - MathF.Abs(2 * l - 1)) * s;
		var hp = h / 60f;
		var x = c * (1 - MathF.Abs(hp % 2 - 1));

		float r1, g1, b1;

		switch (hp)
		{
			case < 1:
				r1 = c;
				g1 = x;
				b1 = 0;
				break;
			case < 2:
				r1 = x;
				g1 = c;
				b1 = 0;
				break;
			case < 3:
				r1 = 0;
				g1 = c;
				b1 = x;
				break;
			case < 4:
				r1 = 0;
				g1 = x;
				b1 = c;
				break;
			case < 5:
				r1 = x;
				g1 = 0;
				b1 = c;
				break;
			default:
				r1 = c;
				g1 = 0;
				b1 = x;
				break;
		}

		var m = l - c / 2f;
		var r = (byte)MathF.Round((r1 + m) * 255f);
		var g = (byte)MathF.Round((g1 + m) * 255f);
		var b = (byte)MathF.Round((b1 + m) * 255f);
		return (r, g, b);
	}

	public static (float h, float s, float l) RgbToHsl(byte r, byte g, byte b)
	{
		var rn = r / 255.0f;
		var gn = g / 255.0f;
		var bn = b / 255.0f;

		var max = Math.Max(rn, Math.Max(gn, bn));
		var min = Math.Min(rn, Math.Min(gn, bn));
		var delta = max - min;

		var h = 0f;

		if (delta != 0)
		{
			if (max == rn)
			{
				h = (gn - bn) / delta % 6;
			}
			else if (max == gn)
			{
				h = (bn - rn) / delta + 2;
			}
			else
			{
				h = (rn - gn) / delta + 4;
			}

			h *= 60;

			if (h < 0)
			{
				h += 360;
			}
		}

		var l = (max + min) / 2;
		var s = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * l - 1));

		return (h, s, l);
	}

	public static float Luminance(byte r, byte g, byte b)
	{
		var rl = Channel(r);
		var gl = Channel(g);
		var bl = Channel(b);

		return 0.2126f * rl + 0.7152f * gl + 0.0722f * bl;

		static float Channel(byte c)
		{
			var cs = c / 255f;

			return cs <= 0.03928f
				? cs / 12.92f
				: MathF.Pow((cs + 0.055f) / 1.055f, 2.4f);
		}
	}

	public static float ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
	{
		var l1 = Luminance(r1, g1, b1);
		var l2 = Luminance(r2, g2, b2);

		if (l1 < l2)
		{
			(l1, l2) = (l2, l1);
		}

		return (l1 + 0.05f) / (l2 + 0.05f);
	}

	public static (byte r, byte g, byte b) BlendRgb(byte rDst, byte gDst, byte bDst,
		byte rSrc, byte gSrc, byte bSrc,
		float alpha, bool gammaCorrect = true)
	{
		alpha = Clamp01(alpha);

		if (gammaCorrect)
		{
			static float ToLinear(byte c)
			{
				var cs = c / 255f;
				return cs <= 0.04045f ? cs / 12.92f : MathF.Pow((cs + 0.055f) / 1.055f, 2.4f);
			}

			static byte ToSrgbByte(float l)
			{
				if (l <= 0f) return 0;
				if (l >= 1f) return 255;

				var cs = l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;

				return (byte)MathF.Round(MathF.Max(0f, MathF.Min(1f, cs)) * 255f);
			}

			var lrDst = ToLinear(rDst);
			var lgDst = ToLinear(gDst);
			var lbDst = ToLinear(bDst);

			var lrSrc = ToLinear(rSrc);
			var lgSrc = ToLinear(gSrc);
			var lbSrc = ToLinear(bSrc);

			var lr = alpha * lrSrc + (1f - alpha) * lrDst;
			var lg = alpha * lgSrc + (1f - alpha) * lgDst;
			var lb = alpha * lbSrc + (1f - alpha) * lbDst;

			return (ToSrgbByte(lr), ToSrgbByte(lg), ToSrgbByte(lb));
		}

		static byte LerpByte(byte a, byte b, float t)
		{
			var fa = a / 255f;
			var fb = b / 255f;
			var fr = t * fb + (1f - t) * fa;

			return (byte)MathF.Round(MathF.Max(0f, MathF.Min(1f, fr)) * 255f);
		}

		var r = LerpByte(rDst, rSrc, alpha);
		var g = LerpByte(gDst, gSrc, alpha);
		var b = LerpByte(bDst, bSrc, alpha);

		return (r, g, b);
	}

	// Additional color operations
	public static (byte r, byte g, byte b) RgbToGrayscale(byte r, byte g, byte b)
	{
		var gray = (byte)((r * 0.299) + (g * 0.587) + (b * 0.114));
		return (gray, gray, gray);
	}

	public static (byte r, byte g, byte b) InvertRgb(byte r, byte g, byte b)
	{
		return ((byte)(255 - r), (byte)(255 - g), (byte)(255 - b));
	}

	public static (byte r, byte g, byte b) AdjustBrightness(byte r, byte g, byte b, float factor)
	{
		factor = MathF.Max(0f, factor);

		var newR = (byte)MathF.Min(255, r * factor);
		var newG = (byte)MathF.Min(255, g * factor);
		var newB = (byte)MathF.Min(255, b * factor);

		return (newR, newG, newB);
	}

	public static (byte r, byte g, byte b) AdjustSaturation(byte r, byte g, byte b, float saturationMultiplier)
	{
		var (h, s, l) = RgbToHsl(r, g, b);
		s = Clamp01(s * saturationMultiplier);
		return HslToRgb(h, s, l);
	}

	public static (byte h, byte s, byte v) RgbToHsv(byte r, byte g, byte b)
	{
		var rn = r / 255.0f;
		var gn = g / 255.0f;
		var bn = b / 255.0f;

		var max = Math.Max(rn, Math.Max(gn, bn));
		var min = Math.Min(rn, Math.Min(gn, bn));
		var delta = max - min;

		var h = 0f;

		if (delta != 0)
		{
			if (max == rn)
			{
				h = 60 * (((gn - bn) / delta) % 6);
			}
			else if (max == gn)
			{
				h = 60 * (((bn - rn) / delta) + 2);
			}
			else
			{
				h = 60 * (((rn - gn) / delta) + 4);
			}

			if (h < 0)
			{
				h += 360;
			}
		}

		var s = max == 0 ? 0 : delta / max;
		var v = max;

		return ((byte)(h * 255 / 360), (byte)(s * 255), (byte)(v * 255));
	}

	public static (byte r, byte g, byte b) HsvToRgb(byte h, byte s, byte v)
	{
		var hf = h * 360.0f / 255;
		var sf = s / 255.0f;
		var vf = v / 255.0f;

		var c = vf * sf;
		var x = c * (1 - MathF.Abs((hf / 60) % 2 - 1));
		var m = vf - c;

		float r1, g1, b1;

		if (hf < 60)
		{
			r1 = c; g1 = x; b1 = 0;
		}
		else if (hf < 120)
		{
			r1 = x; g1 = c; b1 = 0;
		}
		else if (hf < 180)
		{
			r1 = 0; g1 = c; b1 = x;
		}
		else if (hf < 240)
		{
			r1 = 0; g1 = x; b1 = c;
		}
		else if (hf < 300)
		{
			r1 = x; g1 = 0; b1 = c;
		}
		else
		{
			r1 = c; g1 = 0; b1 = x;
		}

		return ((byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
	}

	public static int RgbToHex(byte r, byte g, byte b)
	{
		return (r << 16) | (g << 8) | b;
	}

	public static (byte r, byte g, byte b) HexToRgb(int hex)
	{
		var r = (byte)((hex >> 16) & 0xFF);
		var g = (byte)((hex >> 8) & 0xFF);
		var b = (byte)(hex & 0xFF);
		return (r, g, b);
	}

	private static float Clamp01(float v)
	{
		return v switch
		{
			< 0f => 0f,
			> 1f => 1f,
			_ => v
		};
	}
}

