namespace ConstExpr.Tests.Tests;

public class StringOperationsTest : BaseTest<string>
{
	public override IEnumerable<string> Result => [ "HELLO", "WORLD", "TEST" ];

	public override string SourceCode => """
		using System.Collections.Generic;
		using System.Linq;
		using ConstantExpression;

		namespace Testing;

		public static class Classes
		{
			public void Test()
			{
				Test.ToUpperCase("hello", "world", "test");
			}
			
			[ConstExpr]
			public static IEnumerable<string> ToUpperCase(params string[] values)
			{
				return values.Select(s => s.ToUpper());
			}
		}
		""";
}