namespace ConstExpr.Tests.DateTime;

/// <summary>
///   The DateTime family is not in <c>MethodPurityAnalyzer.PureTypes</c>, but the reflective
///   full-eval path folds its culture-/timezone-independent static methods and
///   <c>new DateTime(...)</c> property chains anyway when every argument is constant.
///   (<c>DateOnly.FromDayNumber(n).DayNumber</c> — a static factory returning a struct followed by
///   a property read — now folds too; see <see cref="DateOnlyFromDayNumberFoldTest" />.)
/// </summary>
[InheritsTests]
public class DateTimeConstantFoldingTest : BaseTest<Func<(bool, int, int)>>
{
	public override string TestMethod => GetString(() => (
		System.DateTime.IsLeapYear(2024),
		System.DateTime.DaysInMonth(2024, 2),
		(int) new System.DateTime(2024, 3, 1).DayOfWeek));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (true, 29, 5);")
	];
}