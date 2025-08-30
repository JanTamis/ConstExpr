namespace ConstExpr.Tests.Tests;

public class VisitDynamicIndexerAccessTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [2];

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
				dynamic b = new List<int> { 1, 2, 3, 4, 5 };
				yield return (int)b[1];
			}
		}
		""";
}
