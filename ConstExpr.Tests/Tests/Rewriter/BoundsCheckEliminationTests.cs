using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Bounds-check elimination on an array parameter: one <c>ref</c> local is hoisted to the top of
///   the body, indexing by <c>0</c> becomes that local, and every other index becomes an
///   <c>Unsafe.Add</c> offset. <c>.Length</c> is left alone.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationTests() : BaseTest<Func<int[], int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(numbers =>
	{
		var sum = numbers[0];

		for (var i = 1; i < numbers.Length; i++)
		{
			sum += numbers[i];
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			var sum = numbersRef;

			for (var i = 1; i < numbers.Length; i++)
			{
				sum += Unsafe.Add(ref numbersRef, (nuint) i);
			}

			return sum;
		}, [ Unknown ]),

		// Known input folds through the interpreter before the pass ever sees an array.
		Create(_ => 15, [ new[] { 1, 2, 3, 4, 5 } ])
	];
}

/// <summary>
///   A string is viewed as a <c>ReadOnlySpan&lt;char&gt;</c> via <c>AsSpan</c>. Reads only — a string
///   has no indexer setter — and <c>.Length</c> stays untouched.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationStringTests() : BaseTest<Func<string, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(text =>
	{
		var sum = text[0];

		for (var i = 1; i < text.Length; i++)
		{
			sum += text[i];
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(text =>
		{
			ref var textRef = ref MemoryMarshal.GetReference(text.AsSpan());
			var sum = textRef;

			for (var i = 1; i < text.Length; i++)
			{
				sum += Unsafe.Add(ref textRef, (nuint) i);
			}

			return sum;
		}, [ Unknown ])
	];
}

/// <summary>
///   The reference is hoisted above a null guard, so the entry point must not dereference.
///   <c>AsSpan</c> maps null to an empty span and only computes an address;
///   <c>string.GetPinnableReference()</c> would throw here instead of letting the guard return, which
///   is why the pass does not use it.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationStringNullGuardTests() : BaseTest<Func<string?, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(text =>
	{
		if (text is null)
		{
			return 0;
		}

		return text[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The guard itself is collapsed to a ternary by an always-on pass; what matters here is that
		// the hoisted reference sits above it and does not fault on a null string.
		Create(text =>
		{
			ref var textRef = ref MemoryMarshal.GetReference(text.AsSpan());

			return text == null ? 0 : textRef;
		}, [ Unknown ])
	];
}