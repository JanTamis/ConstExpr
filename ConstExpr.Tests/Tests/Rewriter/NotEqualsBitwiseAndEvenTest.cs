namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   (x &amp; 1) != 1 folds to T.IsEvenInteger(x). The parentheses are mandatory C# syntax (&amp; binds
///   looser than !=), so this also pins NotEqualsBitwiseAndEvenStrategy's paren-unwrapping.
/// </summary>
[InheritsTests]
public class NotEqualsBitwiseAndEvenTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => (x & 1) != 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Int32.IsEvenInteger(x))
	];
}