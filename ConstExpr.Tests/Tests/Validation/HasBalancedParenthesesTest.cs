using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
///   Regression test: a local mutated inside an if/else-if branch within a foreach must not be
///   const-folded with its initial value after the loop. "(()" ends with balance == 1, so the
///   known-argument fold must produce false, not true.
/// </summary>
[InheritsTests]
public class HasBalancedParenthesesTest() : BaseTest<Func<string?, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(input =>
	{
		if (System.String.IsNullOrEmpty(input))
		{
			return true;
		}

		var balance = 0;

		foreach (var c in input)
		{
			if (c == '(')
			{
				balance++;
			}
			else if (c == ')')
			{
				balance--;

				if (balance < 0)
				{
					return false;
				}
			}
		}

		return balance == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => false, [ "(()" ]),
		Create(_ => true, [ "((()))" ]),
		Create(_ => true, [ "()()()" ]),
		Create(_ => false, [ ")(" ]),
		Create(_ => true, [ System.String.Empty ])
	];
}