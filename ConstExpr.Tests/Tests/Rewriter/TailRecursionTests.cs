using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for Tail-Recursion Elimination (TRE).
///   Tail-recursive methods should be rewritten into an iterative while-loop.
///   Note: TRE runs after ConstExprPartialRewriter.  When both arguments are known the
///   partial rewriter will fully constant-fold the entire recursion before TRE gets a
///   chance to run, so only Unknown-argument cases exercise the rewriter.
/// </summary>
[InheritsTests]
public class TailRecursionTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.TailRecursionElimination)
{
	/// <summary>
	///   Tail-recursive countdown accumulator.
	///   Uses a raw string here because a lambda cannot call itself by name.
	///   The method name must be <c>TestMethod</c> so that BaseTest can find it.
	/// </summary>
	public override string TestMethod => """
		int TestMethod(int n, int acc)
		{
			if (n <= 0)
			{
				return acc;
			}

			return TestMethod(n - 1, acc + n);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: ConstExprPartialRewriter cannot fold the recursion, so TRE
		// rewrites the ternary tail call into a while(true) loop.
		// The partial rewriter first converts if/else to a ternary; TRE then converts
		// that ternary into an if + assignments + continue inside while(true).
		Create("""
			while (true)
			{
				if (n <= 0)
				{
					return acc;
				}

				var _tre_tmp_n = n - 1;
				var _tre_tmp_acc = acc + n;
				n = _tre_tmp_n;
				acc = _tre_tmp_acc;
			}
			""", Unknown, Unknown)
	];
}