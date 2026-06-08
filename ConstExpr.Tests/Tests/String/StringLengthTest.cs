using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLengthTest() : BaseTest<Func<string?, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s =>
	{
		if (s is null)
		{
			return -1;
		}

		return s.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s?.Length ?? -1),
		Create(_ => 0, [ "" ]),
		Create(_ => 11, [ "hello world" ]),
		Create(_ => -1, [ (string?) null ])
	];
}