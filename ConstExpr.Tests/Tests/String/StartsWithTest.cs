using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", "hello", "hel"),
		Create("return false;", "world", "foo"),
		Create("return true;", "", ""),
	];

	public override string TestMethod => """
		bool StartsWith(string s, string prefix)
		{
			return s.StartsWith(prefix);
		}
		""";
}

