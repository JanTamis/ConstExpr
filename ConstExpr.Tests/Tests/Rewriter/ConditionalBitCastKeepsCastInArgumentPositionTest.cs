namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same int-widening shape as <see cref="ConditionalBitCastImplicitWideningToIntTest" />, but as a call
///   argument instead of a direct return. The cast must stay here even though int is implicitly
///   reachable from byte: a bare byte-typed argument can bind to a different overload (or generic
///   inference result) than the original int-typed expression did, so eliding it would silently change
///   which method gets called at a real (overloaded) call site. Confirmed via a standalone repro:
///   Overloads.SomeMethod(cond ? 1 : 0) and Overloads.SomeMethod((int) Unsafe.BitCast&lt;bool, byte&gt;(cond))
///   both call SomeMethod(int); Overloads.SomeMethod(Unsafe.BitCast&lt;bool, byte&gt;(cond)) calls
///   SomeMethod(byte) instead.
/// </summary>
[InheritsTests]
public class ConditionalBitCastKeepsCastInArgumentPositionTest : BaseTest<Func<double, double, int>>
{

	public override string TestMethod => """
		int TestMethod(double x, double y)
		{
			return Identity(x < y ? 1 : 0);
		}

		int Identity(int value) => value;
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Identity((int) Unsafe.BitCast<bool, byte>(x < y));", Unknown, Unknown)
	];
}