using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ReverseStringTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		// Create(null, Unknown),
		// Create("return \"olleh\";", "hello"),
		Create("return \"\";", ""),
		// Create("return \"a\";", "a"),
	];

	public override string TestMethod => """
		string ReverseString(string s)
		{
			var chars = s.ToCharArray();
			var left = 0;
			var right = chars.Length - 1;
			
			while (left < right)
			{
				var temp = chars[left];
				chars[left] = chars[right];
				chars[right] = temp;

				left++;
				right--;
			}
			
			return new string(chars);
		}
		""";
}

