namespace ConstExpr.Tests.Tests;

public class ArithmeticTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [ 5, 8, 15 ];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstantExpression;

		namespace Testing;

		public static class Classes
		{
		 	public void Test()
		 	{
				Test.Calculate(1, 2, 5, 10);
		 	}	
		 	
		 	[ConstExpr]
		 	public static IEnumerable<int> Calculate(params int[] values)
		 	{
		 		return new[] 
		 		{ 
		 			values[0] + values[1] + values[1], 
		 			values[2] + values[1] + values[0],
		 			values[3] + values[2]
		 		};
		 	}
		}
		""";
}