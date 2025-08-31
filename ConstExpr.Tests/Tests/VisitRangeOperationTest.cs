namespace ConstExpr.Tests.Tests;

public class VisitRangeOperationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [2, 3, 4];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using System.Linq;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<int> Run()
			{
				var a = new[] { 1, 2, 3, 4, 5 };
				
				return a[1..^1];
			}
		}
		""";
}
