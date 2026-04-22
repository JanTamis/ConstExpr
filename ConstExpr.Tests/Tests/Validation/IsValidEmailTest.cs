using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsValidEmailTest() : BaseTest<Func<string, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(email =>
	{
		if (string.IsNullOrEmpty(email) || email.Length < 5)
		{
			return false;
		}

		var atCount = 0;
		var dotCount = 0;
		var atIndex = -1;
		var lastDotIndex = -1;

		for (var i = 0; i < email.Length; i++)
		{
			if (email[i] == '@')
			{
				atCount++;
				atIndex = i;
			}
			else if (email[i] == '.')
			{
				dotCount++;
				lastDotIndex = i;
			}
		}

		return atCount == 1 && dotCount >= 1 && atIndex > 0 && atIndex < email.Length - 1 
		       && lastDotIndex > atIndex + 1 && lastDotIndex < email.Length - 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if (String.IsNullOrEmpty(email) || email.Length < 5)
			{
				return false;
			}
			
			var atCount = 0;
			var dotCount = 0;
			var atIndex = -1;
			var lastDotIndex = -1;
			
			for (var i = 0; i < email.Length; i++)
			{
				switch (email[i])
				{
					case '@':
					{
						atCount++;
						atIndex = i;
			
						break;
					}
					case '.':
					{
						dotCount++;
						lastDotIndex = i;
			
						break;
					}
				}
			}
			
			return atCount == 1 && dotCount >= 1 && atIndex > 0 && atIndex < email.Length - 1 && lastDotIndex > atIndex + 1 && lastDotIndex < email.Length - 1;
			"""), // Unknown input → body unchanged
		Create("return false;", ""),                  // Empty string → guard fires
		Create("return false;", "a@b"),               // Too short (length < 5) → guard fires
		Create("return false;", "invalid"),            // No @ or dot → returns false
		Create("return false;", "@test.com"),          // @ at start (atIndex == 0) → returns false
		Create("return false;", "test@com.")           // Dot at end (lastDotIndex == length - 1) → returns false
	];
}

