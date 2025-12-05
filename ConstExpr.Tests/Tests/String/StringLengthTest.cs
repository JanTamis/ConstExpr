using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLengthTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 0;", ""),
		Create("return 11;", "hello world"),
		Create("return -1;", (string?)null),
	];

	public override string TestMethod => """
		int StringLength(string s)
		{
			if (s is null)
			{
				return -1;
			}
			
			return s.Length;
		}
		""";
}

