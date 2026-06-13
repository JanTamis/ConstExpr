extern alias sourcegen;
using sourcegen::ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Validates the arithmetic of the Granlund-Montgomery magic-number computation independently of
///   syntax emission. The body-string tests (<see cref="DivideGranlundMontgomeryUnsignedTest" /> etc.)
///   only confirm the emitted text matches a hand-written magic constant; they never execute the
///   multiply-shift. Here we reconstruct the <em>exact</em> formula emitted by
///   <c>GranlundMontgomeryEmitter</c> and assert it equals native <c>/</c> and <c>%</c> across every
///   branch (add / non-add, signed magic&lt;0, shift&gt;0) and boundary dividend.
/// </summary>
public class GranlundMontgomeryArithmeticTest
{
	// Divisors chosen to span the algorithm's branches and a broad range.
	private static IEnumerable<int> Divisors()
	{
		for (var d = 3; d <= 512; d++)
		{
			if ((d & d - 1) != 0) // skip powers of two (handled by a different strategy)
			{
				yield return d;
			}
		}

		foreach (var d in new[] { 1000, 1023, 1025, 12345, 65535, 65537, 1000000, 1000003 })
		{
			yield return d;
		}
	}

	/// <summary>Mirrors <c>GranlundMontgomeryEmitter.BuildUnsignedQuotient</c> exactly.</summary>
	private static uint UnsignedQuotient(uint x, uint d)
	{
		GranlundMontgomery.ComputeUnsigned(d, out var magic, out var add, out var shift);

		var q0 = (uint) ((ulong) x * magic >> 32);

		if (add)
		{
			return q0 + (x - q0 >> 1) >> shift - 1;
		}

		return shift == 0 ? q0 : q0 >> shift;
	}

	/// <summary>Mirrors <c>GranlundMontgomeryEmitter.BuildSignedQuotient</c> exactly.</summary>
	private static int SignedQuotient(int x, int d)
	{
		GranlundMontgomery.ComputeSigned(d, out var magic, out var shift);

		var q = (int) ((long) x * magic >> 32);

		if (magic < 0)
		{
			q += x;
		}

		if (shift > 0)
		{
			q >>= shift;
		}

		return q - (x >> 31);
	}

	[Test]
	public async Task UnsignedQuotientMatchesNativeDivision()
	{
		var checkedCount = 0L;

		foreach (var di in Divisors())
		{
			var d = (uint) di;

			foreach (var x in UnsignedDividends(d))
			{
				var q = UnsignedQuotient(x, d);

				if (q != x / d || x - q * d != x % d)
				{
					throw new Exception($"Unsigned GM mismatch for x={x}, d={d}: quotient={q} (expected {x / d}), remainder={x - q * d} (expected {x % d}).");
				}

				checkedCount++;
			}
		}

		await Assert.That(checkedCount).IsGreaterThan(0);
	}

	[Test]
	public async Task SignedQuotientMatchesNativeDivision()
	{
		var checkedCount = 0L;

		foreach (var d in Divisors())
		{
			foreach (var x in SignedDividends(d))
			{
				var q = SignedQuotient(x, d);

				if (q != x / d || x - q * d != x % d)
				{
					throw new Exception($"Signed GM mismatch for x={x}, d={d}: quotient={q} (expected {x / d}), remainder={x - q * d} (expected {x % d}).");
				}

				checkedCount++;
			}
		}

		await Assert.That(checkedCount).IsGreaterThan(0);
	}

	private static IEnumerable<uint> UnsignedDividends(uint d)
	{
		// Dense small range.
		for (uint x = 0; x <= 4096; x++)
		{
			yield return x;
		}

		// Around multiples of d.
		for (uint k = 1; k <= 8; k++)
		{
			var m = k * d;
			yield return m - 1;
			yield return m;
			yield return m + 1;
		}

		// Strided sweep across the full uint range.
		for (var x = 0UL; x <= UInt32.MaxValue; x += 0x0020_0001UL)
		{
			yield return (uint) x;
		}

		// Top boundaries.
		yield return UInt32.MaxValue;
		yield return UInt32.MaxValue - 1;
		yield return UInt32.MaxValue - d;
	}

	private static IEnumerable<int> SignedDividends(int d)
	{
		// Dense range straddling zero.
		for (var x = -4096; x <= 4096; x++)
		{
			yield return x;
		}

		// Around ± multiples of d.
		for (var k = 1; k <= 8; k++)
		{
			var m = k * d;
			yield return m - 1;
			yield return m;
			yield return m + 1;
			yield return -(m - 1);
			yield return -m;
			yield return -(m + 1);
		}

		// Strided sweep across the full int range.
		for (var x = (long) Int32.MinValue; x <= Int32.MaxValue; x += 0x0020_0001L)
		{
			yield return (int) x;
		}

		// Boundaries.
		yield return Int32.MinValue;
		yield return Int32.MinValue + 1;
		yield return Int32.MaxValue;
		yield return Int32.MaxValue - 1;
	}
}