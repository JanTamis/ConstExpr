using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests;

[InheritsTests]
public abstract class BaseTestWithRandomValues<TDelegate>(FastMathFlags mathOptimizations = FastMathFlags.All, LinqOptimizationMode linqOptimization = LinqOptimizationMode.Unroll, OptimizationFlags optimizations = OptimizationFlags.All, uint maxUnrollIterations = 32) : BaseTest<TDelegate>(mathOptimizations, linqOptimization, optimizations, maxUnrollIterations)
	where TDelegate : Delegate
{
	/// <summary>
	///   The number of randomly generated test cases <see cref="RunRandomTests" /> checks. Every case is guaranteed to
	///   have a distinct expected body (see <see cref="CreateFoldedRandom" />), so this can't exceed however many
	///   distinct results the tested method can actually produce - override per test class accordingly.
	/// </summary>
	protected virtual int RandomTestCaseCount => 100;

	/// <summary>
	///   Caps the bit-length (and thus magnitude) of randomly generated integral values. Defaults to each type's own
	///   full range. Override to a smaller value for methods whose runtime cost (e.g. loop iteration count) scales
	///   with the parameter's magnitude, so <see cref="BaseTest{TDelegate}" />'s constructor-level
	///   <c>maxUnrollIterations</c> can still fully unroll and fold every generated case.
	/// </summary>
	protected virtual int MaxRandomMagnitudeBits => Int32.MaxValue;

	[Test]
	public void RunRandomTests()
	{
		foreach (var testCase in CreateFoldedRandom().Take(RandomTestCaseCount))
		{
			RunTest(testCase);
		}
	}

	/// <summary>
	///   Lazily generates test cases with randomly generated (fully known) parameter values, using
	///   <see cref="BaseTest{TDelegate}.CreateFolded" /> to compute each expected result by invoking the real
	///   <see cref="BaseTest{TDelegate}.TestMethod" /> delegate. The seed defaults to a stable hash of the test class's type
	///   name, so results are reproducible across runs unless explicitly overridden. Every yielded case has a distinct
	///   expected body - a randomly generated input that throws, or that folds to a body already yielded, is discarded
	///   and retried. If <see cref="MaxConsecutiveMisses" /> attempts in a row fail to produce a new distinct case (the
	///   caller asked for more distinct results than the method can produce, or it throws for nearly all inputs), this
	///   throws instead of spinning forever - callers typically only pull a bounded number via <c>Take</c>.
	/// </summary>
	protected IEnumerable<KeyValuePair<string?, object?[]>> CreateFoldedRandom(int? seed = null)
	{
		var parameterTypes = typeof(TDelegate).GetMethod("Invoke")?.GetParameters().Select(p => p.ParameterType).ToArray()
		                     ?? throw new InvalidOperationException($"Could not resolve Invoke on delegate type '{typeof(TDelegate).FullName}'.");

		var random = new Random(seed ?? GetStableSeed(GetType()));

		while (true)
		{
			var parameters = parameterTypes.Select(t => GenerateRandomValue(t, random)).ToArray();

			yield return CreateFolded(parameters);
		}
	}

	private object GenerateRandomValue(Type type, Random random)
	{
		return type switch
		{
			_ when type == typeof(int) => (int) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(31, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(long) => ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(63, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(short) => (short) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(15, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(sbyte) => (sbyte) ApplyRandomSign(GenerateRandomMagnitude(random, System.Math.Min(7, MaxRandomMagnitudeBits)), random),
			_ when type == typeof(byte) => (byte) GenerateRandomMagnitude(random, System.Math.Min(8, MaxRandomMagnitudeBits)),
			_ when type == typeof(uint) => (uint) GenerateRandomMagnitude(random, System.Math.Min(32, MaxRandomMagnitudeBits)),
			_ when type == typeof(ulong) => (ulong) GenerateRandomMagnitude(random, System.Math.Min(63, MaxRandomMagnitudeBits)),
			_ when type == typeof(ushort) => (ushort) GenerateRandomMagnitude(random, System.Math.Min(16, MaxRandomMagnitudeBits)),
			_ when type == typeof(double) => ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, 20), random),
			_ when type == typeof(float) => (float) ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, 20), random),
			_ when type == typeof(decimal) => (decimal) ApplyRandomSign(GenerateRandomFloatMagnitude(random, -20, 20), random),
			_ when type == typeof(bool) => random.Next(2) == 0,
			_ when type == typeof(char) => GenerateRandomChar(random),
			_ when type == typeof(string) => GenerateRandomString(random),
			_ when type.IsArray => GenerateRandomArray(type.GetElementType()!, random),
			_ when type.IsGenericType && CollectionSupport.GenericTypeDefinitions.Contains(type.GetGenericTypeDefinition())
				=> GenerateRandomList(type.GetGenericArguments()[0], random),
			_ => throw new NotSupportedException($"CreateFoldedRandom does not support generating random values for parameter type '{type}'. Use Create(...)/CreateFolded(...) directly for this test instead.")
		};
	}

	/// <summary>
	///   Generates a random-length (0-8 elements) array of <paramref name="elementType" />, recursively generating
	///   each element via <see cref="GenerateRandomValue" />. Includes the empty array, an important edge case for
	///   most collection-processing functions.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test-only helper; random test-case generation never runs under AOT/trimming.")]
	private System.Array GenerateRandomArray(Type elementType, Random random)
	{
		var length = random.Next(0, 9);
		var array = System.Array.CreateInstance(elementType, length);

		for (var i = 0; i < length; i++)
		{
			array.SetValue(GenerateRandomValue(elementType, random), i);
		}

		return array;
	}

	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test-only helper; random test-case generation never runs under AOT/trimming.")]
	private object GenerateRandomList(Type elementType, Random random)
	{
		var array = GenerateRandomArray(elementType, random);

		return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), array)!;
	}

	private static long ApplyRandomSign(long magnitude, Random random)
	{
		return random.Next(2) == 0 ? magnitude : -magnitude;
	}

	private static double ApplyRandomSign(double magnitude, Random random)
	{
		return random.Next(2) == 0 ? magnitude : -magnitude;
	}

	/// <summary>
	///   Floating-point analogue of <see cref="GenerateRandomMagnitude" />: picks a random power-of-two exponent in
	///   [<paramref name="minExponent" />, <paramref name="maxExponent" />] (each order of magnitude equally likely,
	///   for the same reason integer magnitudes are bit-length scaled rather than drawn linearly), plus one extra
	///   bucket below <paramref name="minExponent" /> that yields exact zero. A mantissa in [1, 2) is then scaled by
	///   2^exponent.
	/// </summary>
	private static double GenerateRandomFloatMagnitude(Random random, int minExponent, int maxExponent)
	{
		var exponent = random.Next(minExponent - 1, maxExponent + 1);

		if (exponent < minExponent)
		{
			return 0d;
		}

		var mantissa = 1 + random.NextDouble();

		return mantissa * System.Math.Pow(2, exponent);
	}

	/// <summary>
	///   Picks a random magnitude on a log/bit-length scale (0..<paramref name="maxBits" /> significant bits, each
	///   equally likely) rather than linearly across the whole range. A linear-uniform draw over e.g. the full
	///   32-bit int range almost always lands in the top one or two decimal-digit buckets (they cover ~47% of the
	///   range), starving small values and producing lots of same-result (duplicate expected body) cases; scaling by
	///   bit-length instead spreads draws roughly evenly across orders of magnitude.
	/// </summary>
	private static long GenerateRandomMagnitude(Random random, int maxBits)
	{
		var bits = random.Next(0, maxBits + 1);

		if (bits == 0)
		{
			return 0;
		}

		var lower = 1L << bits - 1;
		// 1L << bits overflows once bits reaches 63 (there is no 64th magnitude bit in a signed long) - that only
		// happens for the long/ulong case (maxBits == 63); every smaller type's own top bucket is well within range.
		var exclusiveUpper = bits >= 63 ? Int64.MaxValue : 1L << bits;

		return random.NextInt64(lower, exclusiveUpper);
	}

	/// <summary>
	///   Generates a random printable ASCII character (' ' through '~', 0x20-0x7E) - covers lowercase, uppercase,
	///   digits, punctuation, and spaces, while staying free of control characters and surrogate pairs that would
	///   complicate string-processing tests.
	/// </summary>
	private static char GenerateRandomChar(Random random)
	{
		return (char) random.Next(' ', '~' + 1);
	}

	/// <summary>
	///   Generates a random printable-ASCII string, 0-16 characters long. Includes the empty string, an important
	///   edge case for most string-processing functions.
	/// </summary>
	private static string GenerateRandomString(Random random)
	{
		var length = random.Next(0, 17);
		var chars = new char[length];

		for (var i = 0; i < length; i++)
		{
			chars[i] = GenerateRandomChar(random);
		}

		return new string(chars);
	}

	private static int GetStableSeed(Type type)
	{
		unchecked
		{
			var hash = 17;

			foreach (var c in type.FullName ?? type.Name)
			{
				hash = hash * 31 + c;
			}

			return hash;
		}
	}
}

/// <summary>
///   Generic collection interfaces/types that <see cref="BaseTestWithRandomValues{TDelegate}" /> can satisfy with a
///   <see cref="List{T}" /> instance - <see cref="List{T}" /> itself implements all of them, so a single generated
///   list can be handed back regardless of which one the delegate parameter actually declares. Kept as a top-level,
///   non-generic file class so the set isn't duplicated per <c>TDelegate</c> instantiation.
/// </summary>
file static class CollectionSupport
{
	public static readonly HashSet<Type> GenericTypeDefinitions =
	[
		typeof(List<>),
		typeof(IEnumerable<>),
		typeof(IReadOnlyList<>),
		typeof(IReadOnlyCollection<>),
		typeof(ICollection<>)
	];
}