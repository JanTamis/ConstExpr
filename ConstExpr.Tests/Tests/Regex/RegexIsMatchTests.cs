namespace ConstExpr.Tests.Tests.Regex;

[InheritsTests]
public class RegexIsMatchTests() : BaseTest<Func<string, string, bool>>
{
	// Force the assembly to be loaded so CreateCompilation can find it
	private static readonly Type RegexType = typeof(System.Text.RegularExpressions.Regex);

	public override string TestMethod => GetString((value, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown value - both parameters unknown: body remains unchanged
		Create(null, Unknown, Unknown),
		
		// Cijfers
		Create("return Regex_ab3uPQ.IsMatch(value);", Unknown, @"^\d+$"),
		Create("return Regex_pq8O7A.IsMatch(value);", Unknown, @"^\d{4}$"),
		Create("return Regex__XSDpg.IsMatch(value);", Unknown, @"^\d{1,3}$"),

		// Alfanumeriek
		Create("return Regex_qeCgGg.IsMatch(value);", Unknown, @"^[a-z]+$"),
		Create("return Regex_kcCwzA.IsMatch(value);", Unknown, @"^[A-Z]+$"),
		Create("return Regex_VIzc4A.IsMatch(value);", Unknown, @"^[a-zA-Z0-9]+$"),
		Create("return Regex_QdHjEA.IsMatch(value);", Unknown, @"^[a-zA-Z0-9_\-]+$"),

		// E-mail (vereenvoudigd)
		Create("return Regex_Y_nNug.IsMatch(value);", Unknown, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"),

		// URL
		Create("return Regex_q2UJBQ.IsMatch(value);", Unknown, @"^https?://"),
		Create("return Regex_0oT5vQ.IsMatch(value);", Unknown, @"^https?://[^\s/$.?#].[^\s]*$"),

		// Datum formaten
		Create("return Regex_fvr0Tw.IsMatch(value);", Unknown, @"^\d{4}-\d{2}-\d{2}$"),
		Create("return Regex_2LupNA.IsMatch(value);", Unknown, @"^\d{2}/\d{2}/\d{4}$"),

		// Telefoonnummer
		Create("return Regex_6nNw_A.IsMatch(value);", Unknown, @"^\+?[\d\s\-\(\)]{7,15}$"),

		// Postcode (NL)
		Create("return Regex_upkbhA.IsMatch(value);", Unknown, @"^\d{4}\s?[A-Z]{2}$"),

		// IPv4
		Create("return Regex_8FQrVQ.IsMatch(value);", Unknown, @"^(\d{1,3}\.){3}\d{1,3}$"),

		// Hex kleurcode
		Create("return Regex_3Pyq2g.IsMatch(value);", Unknown, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"),

		// Witruimte
		Create("return Regex_8nooBg.IsMatch(value);", Unknown, @"^\s*$"),
		Create("return Regex_3I6I8g.IsMatch(value);", Unknown, @"^\S+$"),

		// Begin/einde patronen
		Create("return Regex_TcVWCQ.IsMatch(value);", Unknown, @"^foo"),
		Create("return Regex_wZ8rMA.IsMatch(value);", Unknown, @"bar$"),

		// Optionele groepen
		Create("return Regex_MZ3CSw.IsMatch(value);", Unknown, @"^colou?r$"),
		Create("return Regex_JAvz2A.IsMatch(value);", Unknown, @"^(foo|bar|baz)$"),

		// Herhalingen
		Create("return Regex_DE13xQ.IsMatch(value);", Unknown, @"^a{3,5}$"),
		Create("return Regex_oXEslQ.IsMatch(value);", Unknown, @"^(ab)+$"),

		// Lookahead / lookbehind
		Create("return Regex_T4GymQ.IsMatch(value);", Unknown, @"(?<=\d)px"),
		Create("return Regex_aTjgeQ.IsMatch(value);", Unknown, @"\d+(?=px)"),

		// Negatieve klasse
		Create("return Regex_sVCbxw.IsMatch(value);", Unknown, @"^[^aeiou]+$"),

		// Multiline / dotall scenario's
		Create("return Regex_qhuJdQ.IsMatch(value);", Unknown, @"(?s)^.+$"),
		Create("return Regex_duxjvg.IsMatch(value);", Unknown, @"(?m)^\w"),

		// Unicode karakters
		Create("return Regex_ys2a4A.IsMatch(value);", Unknown, @"test"),
	];
}