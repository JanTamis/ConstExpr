namespace ConstExpr.Tests.Tests;

public class InterpolatedStringEdgeCasesTest : BaseTest<string>
{
	public override IEnumerable<string> Result => [ 
		"Nested: Hello from John",
		"Math: 3 + 4 = 7",
		"Array: [1, 2, 3]",
		"Object: Person { Name = Alice, Age = 25 }",
		"Escaped: {literal braces}",
		"Multiple: A=10, B=20, Sum=30"
	];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using System.Linq;
		using ConstantExpression;

		namespace Testing;

		public class Person
		{
			public string Name { get; set; }
			public int Age { get; set; }
			
			public override string ToString()
			{
				return $"Person {{ Name = {Name}, Age = {Age} }}";
			}
		}

		public static class Classes
		{
			public void Test()
			{
				Test.NestedInterpolation("John");
				Test.MathExpression(3, 4);
				Test.ArrayToString(new[] { 1, 2, 3 });
				Test.ObjectToString(new Person { Name = "Alice", Age = 25 });
				Test.EscapedBraces();
				Test.MultipleVariables(10, 20);
			}
			
			[ConstExpr]
			public static string NestedInterpolation(string name)
			{
				var greeting = $"Hello from {name}";
				return $"Nested: {greeting}";
			}
			
			[ConstExpr]
			public static string MathExpression(int a, int b)
			{
				return $"Math: {a} + {b} = {a + b}";
			}
			
			[ConstExpr]
			public static string ArrayToString(int[] numbers)
			{
				var joined = string.Join(", ", numbers.Select(n => n.ToString()));
				return $"Array: [{joined}]";
			}
			
			[ConstExpr]
			public static string ObjectToString(Person person)
			{
				return $"Object: {person}";
			}
			
			[ConstExpr]
			public static string EscapedBraces()
			{
				return $"Escaped: {{literal braces}}";
			}
			
			[ConstExpr]
			public static string MultipleVariables(int a, int b)
			{
				var sum = a + b;
				return $"Multiple: A={a}, B={b}, Sum={sum}";
			}
		}
		""";
}
