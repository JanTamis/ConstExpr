namespace ConstExpr.Tests.DateTime;

/// <summary>
///   <c>DateOnly.FromDayNumber(738946)</c> is a static factory returning a <c>DateOnly</c> struct,
///   which is not literal-representable, so the invocation itself never folds. Reading a property off
///   its result (<c>.DayNumber</c>) must still fold: <see cref="ConstExprPartialRewriter" /> re-runs
///   the constant-argument static call reflectively to recover the instance and then evaluates the
///   member access against it.
/// </summary>
[InheritsTests]
public class DateOnlyFromDayNumberFoldTest : BaseTest<Func<int>>
{
	public override string TestMethod => GetString(() => DateOnly.FromDayNumber(738946).DayNumber);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases => [ Create("return 738946;") ];
}