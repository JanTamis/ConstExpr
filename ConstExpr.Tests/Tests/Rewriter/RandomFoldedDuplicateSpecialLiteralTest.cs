namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Regression test for a <c>CreateFoldedRandom</c>/CSE parity gap in the test harness itself
///   (see <see cref="BaseTest{TDelegate}.CreateFoldedSyntax" />): a fully-known result can still
///   contain a structurally repeated literal - here, two independently-computed
///   <c>Double.PositiveInfinity</c>/<c>Double.NegativeInfinity</c> values from dividing by
///   <c>x - x</c>, which is exactly <c>0D</c> for any finite <c>x</c> - and the real pipeline's
///   ordinary CSE pass hoists that repeated literal into a shared local exactly like any other
///   repeated subexpression (a bare <c>MemberAccessExpressionSyntax</c> field reference, e.g.
///   <c>Double.PositiveInfinity</c>, is an ordinary CSE candidate). <c>CreateFoldedSyntax</c> used
///   to build its expected body by directly rendering the naive result without ever running CSE, so
///   it disagreed with the real pipeline's output on structure alone even though the computed values
///   were identical. First found via <see cref="ReciprocalDivisionByMinTest" />'s stable random seed
///   landing on this exact shape.
/// </summary>
[InheritsTests]
public class RandomFoldedDuplicateSpecialLiteralTest : BaseTestWithRandomValues<Func<double, (double, double)>>
{
	public override string TestMethod => GetString(x =>
	{
		var zero = x - x;

		return (1D / zero, 2D / zero);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// x - x folds to 0D even for unknown x under the default FastMathFlags.All (finite-math)
		// assumption, so the whole body is fully known and CSE hoists the repeated
		// Double.PositiveInfinity literal exactly as it would for any other repeated subexpression.
		Create(_ =>
		{
			var DoublePositiveInfinity = Double.PositiveInfinity;

			return (DoublePositiveInfinity, DoublePositiveInfinity);
		})
	];
}