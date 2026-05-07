using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class RemoveWhitespaceTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(input =>
	{
		if (System.String.IsNullOrEmpty(input))
			return input;

		var result = new char[input.Length];
		var index = 0;

		foreach (var c in input)
		{
			if (!Char.IsWhiteSpace(c))
				result[index++] = c;
		}

		return new string(result, 0, index);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if (String.IsNullOrEmpty(input))
				return input;

			var result = new char[input.Length];
			var index = 0;

			foreach (var c in input)
			{
				if (!Char.IsWhiteSpace(c))
					result[index++] = c;
			}

			return new string(result, 0, index);
			"""),
		Create("return \"HelloWorld\";", "Hello World"),
		Create("return \"TestString\";", "  Test  String  "),
		Create("return \"\";", "   "),
		Create("return \"abc\";", "abc")
	];
}