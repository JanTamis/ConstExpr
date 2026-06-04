extern alias sourcegen;
using sourcegen::ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.Tests;

public class TestNameAttribute : DisplayNameFormatterAttribute
{
	protected override string FormatDisplayName(DiscoveredTestContext context)
	{
		var className = context.TestDetails.ClassType.Name;
		var args = context.TestDetails.TestMethodArguments;

		if (args is { Length: > 0 } && args[0] is KeyValuePair<string?, object?[]> pair)
		{
			var values = new string[pair.Value.Length];

			for (var i = 0; i < pair.Value.Length; i++)
			{
				if (SyntaxHelpers.TryCreateLiteral(pair.Value[i], out var literal))
				{
					values[i] = literal.ToString()!;
				}
				else
				{
					values[i] = "Unknown";
				}
			}

			return $"{className}({System.String.Join(", ", values)})";
		}

		return className;
	}
}