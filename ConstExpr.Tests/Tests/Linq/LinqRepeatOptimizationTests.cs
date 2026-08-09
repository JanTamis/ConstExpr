using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for <see cref="Enumerable.Repeat" /> optimization.
///   Verifies that chained LINQ operations on a repeated sequence are rewritten to closed-form expressions:
///   <list type="bullet">
///     <item>
///       <description><c>Repeat(element, count).Count()</c> => <c>count</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Sum()</c> => <c>element * count</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Any()</c> => <c>count &gt; 0</c></description>
///     </item>
///     <item>
///       <description>
///         <c>Repeat(element, count).Contains(x)</c> => <c>count &gt; 0 &amp;&amp; element == x</c>
///       </description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).First()</c> => <c>count &gt; 0 ? element : throw</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Last()</c> => <c>count &gt; 0 ? element : throw</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).ElementAt(n)</c> => <c>n &lt; count ? element : throw</c></description>
///     </item>
///     <item>
///       <description>
///         <c>Repeat(element, count).ElementAtOrDefault(n)</c> =>
///         <c>n &gt;= 0 &amp;&amp; n &lt; count ? element : default</c>
///       </description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Min()</c> => <c>count &gt; 0 ? element : throw</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Max()</c> => <c>count &gt; 0 ? element : throw</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Skip(n).Count()</c> => <c>Int32.Max(0, count - n)</c></description>
///     </item>
///     <item>
///       <description><c>Repeat(element, count).Take(n).Count()</c> => <c>Int32.Min(n, count)</c></description>
///     </item>
///     <item>
///       <description>
///         <c>Repeat(element, count).All(predicate)</c> => <c>count &lt;= 0 || predicate(element)</c>
///       </description>
///     </item>
///   </list>
///   When all arguments are constant, all expressions fold to a single numeric literal.
/// </summary>
[InheritsTests]
public class LinqRepeatOptimizationTests : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((element, count) =>
	{
		// Repeat(element, count).Count() => count
		var a = Enumerable.Repeat(element, count).Count();

		// Repeat(element, count).Sum() => element * count
		var b = Enumerable.Repeat(element, count).Sum();

		// Repeat(element, count).Any() => count > 0
		var c = Enumerable.Repeat(element, count).Any() ? 1 : 0;

		// Repeat(element, count).Contains(5) => count > 0 && element == 5
		var d = Enumerable.Repeat(element, count).Contains(5) ? 1 : 0;

		// Repeat(element, count).First() => count > 0 ? element : throw
		var e = Enumerable.Repeat(element, count).First();

		// Repeat(element, count).Last() => count > 0 ? element : throw
		var f = Enumerable.Repeat(element, count).Last();

		// Repeat(element, count).ElementAt(2) => 2 < count ? element : throw
		var g = Enumerable.Repeat(element, count).ElementAt(2);

		// Repeat(element, count).ElementAtOrDefault(2) => 2 < count ? element : 0
		var l = Enumerable.Repeat(element, count).ElementAtOrDefault(2);

		// Repeat(element, count).Min() => count > 0 ? element : throw
		var h = Enumerable.Repeat(element, count).Min();

		// Repeat(element, count).Max() => count > 0 ? element : throw
		var i = Enumerable.Repeat(element, count).Max();

		// Repeat(element, count).Skip(2).Count() => Int32.Max(0, count - 2)
		var j = Enumerable.Repeat(element, count).Skip(2).Count();

		// Repeat(element, count).Take(2).Count() => Int32.Min(2, count)
		var k = Enumerable.Repeat(element, count).Take(2).Count();

		// Repeat(element, count).All(x => x > 0) => count <= 0 || element > 0
		var m = Enumerable.Repeat(element, count).All(x => x > 0) ? 1 : 0;

		return a + b + c + d + e + f + g + l + h + i + j + k + m;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((element, count) =>
		{
			var gt = count > 0;

			return ((gt ? element : ThrowInvalidOperationException<int>("Sequence contains no elements")) << 2) + count + element * count + Unsafe.BitCast<bool, byte>(gt) + Unsafe.BitCast<bool, byte>(gt && element == 5) + (count > 2 ? element : ThrowArgumentOutOfRangeException<int>("")) + element + Int32.Max(0, count - 2) + Int32.Min(2, count) + Unsafe.BitCast<bool, byte>(count <= 0 || element > 0);
		}),
		CreateFolded(3, 4)
	];

	// Local stand-ins for the private helpers the generator extracts throw-expressions into (see
	// ThrowExpressionExtractionRewriter) — needed so the expected-body lambda above compiles; only their
	// call syntax is compared, not these bodies. Generic because a throw expression only ever occupies a
	// value position (cond ? a : throw ...), so the extracted helper can't be void.
	[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
	private static T ThrowInvalidOperationException<T>(string message)
	{
		throw new InvalidOperationException(message);
	}

	[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
	private static T ThrowArgumentOutOfRangeException<T>(string message)
	{
		throw new ArgumentOutOfRangeException(message);
	}
}