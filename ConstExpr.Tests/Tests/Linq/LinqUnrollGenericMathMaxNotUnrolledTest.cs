using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Regression: <c>int.Max(a, b)</c> (generic-math <c>Int32.Max</c>, receiver is the <c>int</c>
///   keyword) must not be mistaken for <c>Enumerable.Max()</c> and unrolled as a sequence.
///   <c>LinqUnroller.ParseLinqChain</c> already rejected the <c>Int32.Max</c> spelling; it now also
///   rejects the <see cref="PredefinedTypeSyntax" /> (<c>int</c>/<c>long</c>/...) form.
///   Runs under <see cref="FastMathFlags.Strict" /> so the math Max optimizer stays off the value and
///   the call is left verbatim.
/// </summary>
[InheritsTests]
public class LinqUnrollGenericMathMaxNotUnrolledTest()
	: BaseTest<Func<int, int, int>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((a, b) => Int32.Max(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}