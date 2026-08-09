using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsValidEmailTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(email =>
	{
		if (System.String.IsNullOrEmpty(email) || email.Length < 5)
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
		Create(email =>
		{
			ref var emailRef = ref MemoryMarshal.GetReference(email.AsSpan());
			var emailLength = email.Length;

			if (emailLength is 0 or < 5)
				return false;

			var atCount = 0;
			var dotCount = 0;
			var atIndex = -1;
			var lastDotIndex = -1;

			for (var i = 0; i < emailLength; i++)
			{
				switch (Unsafe.Add(ref emailRef, i))
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

			return atCount == 1 && dotCount >= 1 && atIndex > 0 && atIndex < emailLength - 1 && lastDotIndex > atIndex + 1 && lastDotIndex < emailLength - 1;
		}), // Unknown input → body unchanged
		CreateFolded(System.String.Empty), // Empty string → guard fires
		CreateFolded("a@b"), // Too short (length < 5) → guard fires
		CreateFolded("invalid"), // No @ or dot → returns false
		CreateFolded("@test.com"), // @ at start (atIndex == 0) → returns false
		CreateFolded("test@com.") // Dot at end (lastDotIndex == length - 1) → returns false
	];
}