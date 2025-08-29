namespace ConstExpr.Tests.Tests;

public class VisitWhileLoopTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [3];

	public override string SourceCode => """
		using System.Collections.Generic;
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
				int i = 0;
				int count = 0;
				while (i < 3) // VisitWhileLoop
				{
					i++;
					count++;
				}
				yield return count;
			}
		}
		""";
}
