namespace ConstExpr.Tests.Tests;

public class VisitDynamicIndexerAccessTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [7];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public class B
		{
			public int this[int i] => i + 6;
		}

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<int> Run()
			{
				dynamic b = new B();
				yield return (int)b[1];
			}
		}
		""";
}
