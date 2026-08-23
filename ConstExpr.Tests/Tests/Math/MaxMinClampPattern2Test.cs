namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxMinClampPattern2Test : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(value => Int32.Max(0, Int32.Min(value, 10)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(value => Int32.Clamp(value, 0, 10))
	];
}