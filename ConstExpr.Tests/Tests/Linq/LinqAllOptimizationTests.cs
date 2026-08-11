namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for All() optimization - verify that unnecessary operations before All() are removed
///   and Where predicates are combined with All predicates
/// </summary>
[InheritsTests]
public class LinqAllOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).All() => All(combined predicates)
		var a = x.Where(v => v > 0).All(v => v < 10) ? 1 : 0;

		// Select(...).All() => All()
		var b = x.Select(v => v * 2).All(v => v > 0) ? 1 : 0;

		// Distinct().All() => All()
		var c = x.Distinct().All(v => v > 0) ? 1 : 0;

		// OrderBy(...).All() => All()
		var d = x.OrderBy(v => v).All(v => v > 0) ? 1 : 0;

		// OrderByDescending(...).All() => All()
		var e = x.OrderByDescending(v => v).All(v => v > 0) ? 1 : 0;

		// Reverse().All() => All()
		var f = x.Reverse().All(v => v > 0) ? 1 : 0;

		// AsEnumerable().All() => All()
		var g = x.AsEnumerable().All(v => v > 0) ? 1 : 0;

		// ToList().All() => All()
		var h = x.ToList().All(v => v > 0) ? 1 : 0;

		// ToArray().All() => All()
		var i = x.ToArray().All(v => v > 0) ? 1 : 0;

		// All elements satisfy condition
		var j = x.All(v => v > 0) ? 1 : 0;

		// No elements satisfy condition
		var k = x.Concat(x).All(v => v > 100) ? 1 : 0;

		// Complex: OrderBy().Where().All() => All(combined)
		var l = x.OrderBy(v => v).Where(v => v > 2).All(v => v < 8) ? 1 : 0;

		var m = x.Append(5).All(v => v > 3) ? 1 : 0;

		var n = x.Prepend(5).All(v => v > 3) ? 1 : 0;

		var o = x.DefaultIfEmpty().All(v => v > 3) ? 1 : 0;

		var p = x.DefaultIfEmpty(5).All(v => v > 3) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o + p;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var callVal = VectorOperations.All<int, OperatorkNzSYw>(x);

			return (Unsafe.BitCast<bool, byte>(VectorOperations.All<int, OperatorF8nEFw>(x)) << 3) + Unsafe.BitCast<bool, byte>(VectorOperations.All<int, Operator2sjEFw>(x)) * 3 + Unsafe.BitCast<bool, byte>(VectorOperations.All<int, OperatoronoWMw>(x)) + Unsafe.BitCast<bool, byte>(VectorOperations.All<int, OperatoriNrkaQ>(x)) + Unsafe.BitCast<bool, byte>(callVal && callVal) + Unsafe.BitCast<bool, byte>(VectorOperations.All<int, OperatorJtMU7Q>(x));
			"""),
	];
}