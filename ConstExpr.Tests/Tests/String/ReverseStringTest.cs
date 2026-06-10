using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ReverseStringTest() : BaseTest<Func<string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s =>
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
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => "olleh", [ "hello" ]),
		Create(_ => "", [ "" ]),
		Create(_ => "a", [ "a" ])
	];
}