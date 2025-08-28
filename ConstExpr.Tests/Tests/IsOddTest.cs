namespace ConstExpr.Tests.Tests;

public class IsOddTest : BaseTest<float>
{
	public override IEnumerable<float> Result => [1f, 3f, 5f];

	public override string SourceCode => """
		using System.Collections.Generic;
		using System.Linq;
		using ConstExpr.Core.Attributes;
		
		namespace Testing;
		
		public static class Classes
		{
			public static void Test()
			{
				IsOdd(1f, 2f, 3f, 4f, 5f);
			}
			
			[ConstExpr]
			public static IEnumerable<float> IsOdd(params IEnumerable<float> data)
			{
				return data
					.Where(w => w % 2 != 0);
			}
		}
		""";
}