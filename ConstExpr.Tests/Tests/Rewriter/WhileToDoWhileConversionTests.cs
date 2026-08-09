using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for while-to-do-while conversion (WDW).
///   An early-exit guard (<c>if (n &lt;= 0) return 0;</c>) narrows <c>n</c> to <c>&gt;= 1</c>, and the
///   counter <c>i</c> starts at the literal <c>1</c> right before the loop, so <c>i &lt;= n</c> is
///   proven true on the very first check and the <c>while</c> becomes a <c>do</c>-<c>while</c>.
/// </summary>
[InheritsTests]
public class WhileToDoWhileConversionTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.WhileToDoWhileConversion)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var i = 1;
		var count = 0;

		while (i <= n)
		{
			count++;
			i++;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			if (n <= 0)
			{
				return 0;
			}

			var i = 1;
			var count = 0;

			do
			{
				count++;
				i++;
			} while (i <= n);

			return count;
		}, [ Unknown ]),
		CreateFolded(3),
		CreateFolded(0),
		CreateFolded(1)
	];
}