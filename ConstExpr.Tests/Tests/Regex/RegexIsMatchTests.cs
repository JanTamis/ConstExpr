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
		// Cijfers
		Create(null, Unknown, @"^\d+$"),
		Create(null, Unknown, @"^\d{4}$"),
		Create(null, Unknown, @"^\d{1,3}$"),

		// Alfanumeriek
		Create(null, Unknown, @"^[a-z]+$"),
		Create(null, Unknown, @"^[A-Z]+$"),
		Create(null, Unknown, @"^[a-zA-Z0-9]+$"),
		Create(null, Unknown, @"^[a-zA-Z0-9_\-]+$"),

		// E-mail (vereenvoudigd)
		Create(null, Unknown, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"),

		// URL
		Create(null, Unknown, @"^https?://"),
		Create(null, Unknown, @"^https?://[^\s/$.?#].[^\s]*$"),

		// Datum formaten
		Create(null, Unknown, @"^\d{4}-\d{2}-\d{2}$"),
		Create(null, Unknown, @"^\d{2}/\d{2}/\d{4}$"),

		// Telefoonnummer
		Create(null, Unknown, @"^\+?[\d\s\-\(\)]{7,15}$"),

		// Postcode (NL)
		Create(null, Unknown, @"^\d{4}\s?[A-Z]{2}$"),

		// IPv4
		Create(null, Unknown, @"^(\d{1,3}\.){3}\d{1,3}$"),

		// Hex kleurcode
		Create(null, Unknown, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"),

		// Witruimte
		Create(null, Unknown, @"^\s*$"),
		Create(null, Unknown, @"^\S+$"),

		// Begin/einde patronen
		Create(null, Unknown, @"^foo"),
		Create(null, Unknown, @"bar$"),

		// Optionele groepen
		Create(null, Unknown, @"^colou?r$"),
		Create(null, Unknown, @"^(foo|bar|baz)$"),

		// Herhalingen
		Create(null, Unknown, @"^a{3,5}$"),
		Create(null, Unknown, @"^(ab)+$"),

		// Lookahead / lookbehind
		Create(null, Unknown, @"(?<=\d)px"),
		Create(null, Unknown, @"\d+(?=px)"),

		// Negatieve klasse
		Create(null, Unknown, @"^[^aeiou]+$"),

		// Multiline / dotall scenario's
		Create(null, Unknown, @"(?s)^.+$"),
		Create(null, Unknown, @"(?m)^\w"),
	];
}