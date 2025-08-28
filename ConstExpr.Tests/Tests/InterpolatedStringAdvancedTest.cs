namespace ConstExpr.Tests.Tests;

public class InterpolatedStringAdvancedTest : BaseTest<string>
{
	public override IEnumerable<string> Result => [ 
		"Hex: 0xFF", 
		"Binary: 1010", 
		"Date: 2023-12-25", 
		"Time: 14:30", 
		"Multi-line\nString with\nbreaks",
		"Empty: ",
		"Null: ",
		"Conditional: Yes"
	];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				HexFormatting(255);
				BinaryFormatting(10);
				DateFormatting(new DateTime(2023, 12, 25));
				TimeFormatting(new TimeSpan(14, 30, 0));
				MultiLineString();
				EmptyString();
				NullString();
				ConditionalString(true);
			}
			
			[ConstExpr]
			public static string HexFormatting(int value)
			{
				return $"Hex: 0x{value:X}";
			}
			
			[ConstExpr]
			public static string BinaryFormatting(int value)
			{
				return $"Binary: {Convert.ToString(value, 2)}";
			}
			
			[ConstExpr]
			public static string DateFormatting(DateTime date)
			{
				return $"Date: {date:yyyy-MM-dd}";
			}
			
			[ConstExpr]
			public static string TimeFormatting(TimeSpan time)
			{
				return $"Time: {time:hh\\:mm}";
			}
			
			[ConstExpr]
			public static string MultiLineString()
			{
				var part1 = "Multi-line";
				var part2 = "String with";
				var part3 = "breaks";
				return $"{part1}\n{part2}\n{part3}";
			}
			
			[ConstExpr]
			public static string EmptyString()
			{
				var empty = "";
				return $"Empty: {empty}";
			}
			
			[ConstExpr]
			public static string NullString()
			{
				string nullStr = null;
				return $"Null: {nullStr}";
			}
			
			[ConstExpr]
			public static string ConditionalString(bool condition)
			{
				return $"Conditional: {(condition ? "Yes" : "No")}";
			}
		}
		""";
}
