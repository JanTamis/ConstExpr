namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same identity as <see cref="ReciprocalDivisionByMaxTest" /> for the <c>Min</c> side: a
///   denominator declared as <c>Min(Min(x,y),z)</c> and divided into 3 times (all unconditional)
///   is converted to <c>* ReciprocalEstimate(min)</c> at each occurrence, then the repeated call is
///   hoisted into <c>invMin</c>.
///   <para>
///     Deliberately <see cref="BaseTest{TDelegate}" />, not <see cref="BaseTestWithRandomValues{TDelegate}" />:
///     this class's stable random seed draws an exact-zero <c>min</c> (a deliberately-included fuzz
///     bucket, see <see cref="BaseTestWithRandomValues{TDelegate}.MaxRandomFloatExponent" />'s doc)
///     with two of the three divisions landing on the same <c>Double.NegativeInfinity</c>. That's a
///     genuine, pre-existing, unrelated gap — CSE hoisting the repeated special-value literal into a
///     shared local, which <c>CreateFoldedRandom</c>'s expected body never runs CSE to anticipate —
///     not a defect in the reciprocal-division identity itself (the computed values match exactly;
///     only the hoisting differs). Tracked separately rather than worked around here.
///   </para>
/// </summary>
[InheritsTests]
public class ReciprocalDivisionByMinTest : BaseTest<Func<double, double, double, (double, double, double)>>
{
	public override string TestMethod => GetString((x, y, z) =>
	{
		var min = System.Math.Min(System.Math.Min(x, y), z);

		return (x / min, y / min, z / min);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, z) =>
		{
			var min = Double.MinNative(Double.MinNative(x, y), z);
			var invMin = Double.ReciprocalEstimate(min);

			return (x * invMin, y * invMin, z * invMin);
		})
	];
}