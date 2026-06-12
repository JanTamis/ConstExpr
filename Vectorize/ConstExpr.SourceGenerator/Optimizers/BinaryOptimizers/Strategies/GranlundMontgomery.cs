using System;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Granlund-Montgomery magic-number computation for replacing division (and the
///   quotient used by modulo) by a constant with a multiply-high + shift sequence.
///   Mirrors the well-known algorithms from Hacker's Delight (2nd ed.), §10-9
///   (<c>magicu</c>) and §10-6 (<c>magic</c>), specialised to 32-bit operands.
///   The same magic is shared between the division and modulo strategies so that
///   <c>x / d</c> and <c>x % d</c> emit consistent constants (modulo is derived as
///   <c>x - d * (x / d)</c>).
/// </summary>
internal static class GranlundMontgomery
{
	/// <summary>
	///   Computes the unsigned 32-bit magic for division by <paramref name="d" />.
	///   The quotient of <c>x / d</c> for <c>uint x</c> is:
	///   <code>
	///   q0 = (uint)((ulong)x * magic >> 32);
	///   q  = add ? ((q0 + ((x - q0) >> 1)) >> (shift - 1))
	///            : (q0 >> shift);
	/// </code>
	/// </summary>
	public static void ComputeUnsigned(uint d, out uint magic, out bool add, out int shift)
	{
		add = false;

		unchecked
		{
			var nc = 0xFFFFFFFFu - (0u - d) % d;
			var p = 31;
			var q1 = 0x80000000u / nc;
			var r1 = 0x80000000u - q1 * nc;
			var q2 = 0x7FFFFFFFu / d;
			var r2 = 0x7FFFFFFFu - q2 * d;
			uint delta;

			do
			{
				p++;

				if (r1 >= nc - r1)
				{
					q1 = 2 * q1 + 1;
					r1 = 2 * r1 - nc;
				}
				else
				{
					q1 = 2 * q1;
					r1 = 2 * r1;
				}

				if (r2 + 1 >= d - r2)
				{
					if (q2 >= 0x7FFFFFFFu)
					{
						add = true;
					}

					q2 = 2 * q2 + 1;
					r2 = 2 * r2 + 1 - d;
				}
				else
				{
					if (q2 >= 0x80000000u)
					{
						add = true;
					}

					q2 = 2 * q2;
					r2 = 2 * r2 + 1;
				}

				delta = d - 1 - r2;
			} while (p < 64 && (q1 < delta || q1 == delta && r1 == 0));

			magic = q2 + 1;
			shift = p - 32;
		}
	}

	/// <summary>
	///   Computes the signed 32-bit magic for division by <paramref name="d" /> (with <c>|d| &gt;= 2</c>).
	///   The quotient of <c>x / d</c> for <c>int x</c> is:
	///   <code>
	///   q = (int)((long)x * magic >> 32);
	///   if (d > 0 &amp;&amp; magic &lt; 0) q += x;   // magic wrapped negative
	///   if (d &lt; 0 &amp;&amp; magic > 0) q -= x;
	///   q >>= shift;                          // arithmetic
	///   q += (q >>> 31);                       // round toward zero for negatives
	/// </code>
	/// </summary>
	public static void ComputeSigned(int d, out int magic, out int shift)
	{
		unchecked
		{
			var ad = (uint) Math.Abs((long) d);
			const uint two31 = 0x80000000u;
			var t = two31 + ((uint) d >> 31);
			var anc = t - 1 - t % ad;
			var p = 31;
			var q1 = two31 / anc;
			var r1 = two31 - q1 * anc;
			var q2 = two31 / ad;
			var r2 = two31 - q2 * ad;
			uint delta;

			do
			{
				p++;

				q1 = 2 * q1;
				r1 = 2 * r1;

				if (r1 >= anc)
				{
					q1++;
					r1 -= anc;
				}

				q2 = 2 * q2;
				r2 = 2 * r2;

				if (r2 >= ad)
				{
					q2++;
					r2 -= ad;
				}

				delta = ad - r2;
			} while (q1 < delta || q1 == delta && r1 == 0);

			magic = (int) (q2 + 1);

			if (d < 0)
			{
				magic = -magic;
			}

			shift = p - 32;
		}
	}
}