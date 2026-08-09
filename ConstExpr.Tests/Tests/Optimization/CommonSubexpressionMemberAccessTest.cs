using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

// Coverage: `s.Length` is a plain member access rooted in an identifier — previously never a CSE
// candidate at all, since the eliminator had no way to prove a property getter is side-effect-free.
// Now backed by a SemanticModel + MethodPurityAnalyzer: string.Length is on the known-pure type
// whitelist, so two reads of the same expression collapse into one hoisted local.
[InheritsTests]
public class CommonSubexpressionMemberAccessTest() : BaseTest<Func<string, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString(s =>
	{
		var a = s.Length;
		var b = s.Length;
		return a * b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s =>
		{
			var sLength = s.Length;

			return sLength * sLength;
		}),
		CreateFolded("abc")
	];
}