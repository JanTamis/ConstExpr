namespace ConstExpr.Tests.Tests;

public class DateTimeTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [ 3 ];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstantExpression;

		namespace Testing;

		public static class Classes
		{
			public void Test()
			{
				Test.DaysBetween(new DateTime(2023, 1, 1), new DateTime(2023, 1, 4));
			}
			
			[ConstExpr]
			public static IEnumerable<int> DaysBetween(DateTime start, DateTime end)
			{
				return new[] { (end - start).Days };
			}
		}
		""";
}