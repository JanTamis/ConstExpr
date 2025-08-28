namespace ConstExpr.Tests.Tests;

public class ComplexLinqTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [ 6, 4, 2 ];

	public override string SourceCode => """
		using System.Collections.Generic;
		using System.Linq;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				ProcessNumbers(1, 2, 3, 4, 5, 6);
			}
			
			[ConstExpr]
			public static IEnumerable<int> ProcessNumbers(params int[] values)
			{
				return values
					.Where(x => x % 2 == 0)
			    .OrderByDescending(x => x)
			    .Take(3);
			}
		}
		""";
}