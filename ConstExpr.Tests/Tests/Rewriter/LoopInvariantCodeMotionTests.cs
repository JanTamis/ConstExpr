using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for Loop Invariant Code Motion (LICM).
///   Declarations inside a loop whose initialisers do not depend on
///   any variable modified in the loop body should be hoisted to before the loop.
///   Note: LICM runs after ConstExprPartialRewriter.
///   - The partial rewriter may already have applied binary optimisations (e.g. angle * 3 →
///   ((angle &lt;&lt; 1) + angle)).
///   - Single-use variables are inlined by the partial rewriter, so test cases use a
///   multi-use invariant variable (used twice per iteration) which is NOT inlined.
///   - WrapWithHoisted produces an enclosing block around the loop when invariants are
///   hoisted; the expected body reflects this shape.
/// </summary>
[InheritsTests]
public class LoopInvariantCodeMotionTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.LoopInvariantCodeMotion)
{
	/// <summary>
	///   An invariant expression used twice per iteration: not inlined by the partial
	///   rewriter, so LICM hoists it out of the loop.
	/// </summary>
	public override string TestMethod => GetString((n, angle) =>
	{
		var result = 0;

		for (var i = 0; i < n; i++)
		{
			var step = angle * 3; // used twice → won't be inlined by partial rewriter
			result += step;
			result += step;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: LICM hoists the multi-use invariant declaration.
		// The partial rewriter rewrites angle * 3 to ((angle << 1) + angle).
		// WrapWithHoisted wraps the loop in an extra block together with the hoisted var.
		Create((n, angle) =>
		{
			var result = 0;
			var step = (angle << 1) + angle;

			for (var i = 0; i < n; i++)
			{
				result += step;
				result += step;
			}

			return result;
		}, [ Unknown, Unknown ])
	];
}