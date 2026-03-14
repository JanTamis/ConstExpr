namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for <see cref="Enumerable.Range"/> optimization.
/// Verifies that chained LINQ operations on a range are rewritten to closed-form arithmetic expressions:
/// <list type="bullet">
/// <item><description><c>Range(start, count).Count()</c> => <c>count</c></description></item>
/// <item><description><c>Range(start, count).Sum()</c> => <c>count * (2 * start + count - 1) / 2</c></description></item>
/// <item><description><c>Range(start, count).Any()</c> => <c>count &gt; 0</c></description></item>
/// <item><description><c>Range(start, count).Contains(x)</c> => <c>x &gt;= start &amp;&amp; x &lt; start + count</c></description></item>
/// <item><description><c>Range(start, count).First()</c> => <c>start</c></description></item>
/// <item><description><c>Range(start, count).Last()</c> => <c>start + count - 1</c></description></item>
/// <item><description><c>Range(start, count).ElementAt(n)</c> => <c>start + n</c></description></item>
/// <item><description><c>Range(start, count).Average()</c> => <c>Double.MultiplyAddEstimate(count - 1, 0.5, start)</c></description></item>
/// <item><description><c>Range(start, count).Min()</c> => <c>start</c></description></item>
/// <item><description><c>Range(start, count).Max()</c> => <c>start + count - 1</c></description></item>
/// <item><description><c>Range(start, count).Skip(n).Count()</c> => <c>Int32.Max(0, count - n)</c></description></item>
/// <item><description><c>Range(start, count).Take(n).Count()</c> => <c>Int32.Min(n, count)</c></description></item>
/// </list>
/// When all arguments are constant, all expressions fold to a single numeric literal.
/// </summary>
[InheritsTests]
public class LinqRangeOptimizationTests : BaseTest<Func<int, int, double>>
{
	public override string TestMethod => GetString((start, count) =>
	{
		// Range(start, count).Count() => count
		var a = Enumerable.Range(start, count).Count();

		// Range(start, count).Sum() => count * (2 * start + count - 1) / 2
		var b = Enumerable.Range(start, count).Sum();
		
		// Range(start, count).Any() => count > 0
		var c = Enumerable.Range(start, count).Any() ? 1 : 0;
		
		// Range(start, count).Contains(x) => x >= start && x < start + count
		var d = Enumerable.Range(start, count).Contains(5) ? 1 : 0;
		
		// Range(start, count).First() => start
		var e = Enumerable.Range(start, count).First();
		
		// Range(start, count).Last() => start + count - 1
		var f = Enumerable.Range(start, count).Last();
		
		// Range(start, count).ElementAt(n) => start + n
		var g = Enumerable.Range(start, count).ElementAt(2);

		// Range(start, count).Average() => Double.MultiplyAddEstimate(count - 1, 0.5, start)
		var h = Enumerable.Range(start, count).Average();
		
		// Range(start, count).Min() => start
		var i = Enumerable.Range(start, count).Min();
		
		// Range(start, count).Max() => start + count - 1 
		var j = Enumerable.Range(start, count).Max();

		// Range(start, count).Skip(n).Count() => Int32.Max(0, count - n)
		var k = Enumerable.Range(start, count).Skip(2).Count();
		
		// Range(start, count).Take(n).Count() => Int32.Min(n, count)
		var l = Enumerable.Range(start, count).Take(2).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var b = count * (start << 1 + count - 1) / 2;
			var c = count > 0 ? 1 : 0;
			var d = 5 >= start && 5 < count + start ? 1 : 0;
			var f = start + count - 1;
			var g = start + 2;
			var h = Double.MultiplyAddEstimate(count - 1, 0.5, start);
			var j = start + count - 1;
			var k = Int32.Max(0, count - 2);
			var l = Int32.Min(2, count);
			
			return count + b + c + d + start + f + g + h + start + j + k + l;
			""", Unknown, Unknown),
		Create("return 56D;", 2, 5),
	];
}

