namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for conditional AND/OR optimizer strategies.</summary>
[InheritsTests]
public class ConditionalAndWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return b;"),
		Create("return true;", true),
		Create("return false;", false),
	];
}

/// <summary>b &amp;&amp; false = false.</summary>
[InheritsTests]
public class ConditionalAndWithFalseTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return false;"),
		Create("return false;", true),
		Create("return false;", false),
	];
}

/// <summary>b || true = true.</summary>
[InheritsTests]
public class ConditionalOrWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return true;"),
		Create("return true;", true),
		Create("return true;", false),
	];
}

/// <summary>b || false = b.</summary>
[InheritsTests]
public class ConditionalOrWithFalseTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return b;"),
		Create("return true;", true),
		Create("return false;", false),
	];
}

/// <summary>b &amp;&amp; b = b (idempotency).</summary>
[InheritsTests]
public class ConditionalAndIdempotencyTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return b;"),
		Create("return true;", true),
		Create("return false;", false),
	];
}

/// <summary>b || b = b (idempotency).</summary>
[InheritsTests]
public class ConditionalOrIdempotencyTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return b;"),
		Create("return true;", true),
		Create("return false;", false),
	];
}
