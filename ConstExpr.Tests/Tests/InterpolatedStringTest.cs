namespace ConstExpr.Tests.Tests;

public class InterpolatedStringTest : BaseTest<string>
{
	public override IEnumerable<string> Result => [ "Hello, World!", "Value: 42", "Count: 5", "Price: $123.45", "Person: John (Age: 30)" ];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				BasicInterpolation("World");
				NumericInterpolation(42);
				ExpressionInterpolation(new[] { 1, 2, 3, 4, 5 });
				FormattedInterpolation(123.45m);
				ComplexInterpolation("John", 30);
			}
			
			[ConstExpr]
			public static string BasicInterpolation(string name)
			{
				return $"Hello, {name}!";
			}
			
			[ConstExpr]
			public static string NumericInterpolation(int value)
			{
				return $"Value: {value}";
			}
			
			[ConstExpr]
			public static string ExpressionInterpolation(int[] items)
			{
				return $"Count: {items.Length}";
			}
			
			[ConstExpr]
			public static string FormattedInterpolation(decimal price)
			{
				return $"Price: ${price:F2}";
			}
			
			[ConstExpr]
			public static string ComplexInterpolation(string name, int age)
			{
				return $"Person: {name} (Age: {age})";
			}
		}
		""";
}
