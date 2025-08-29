namespace ConstExpr.Tests.Tests;

public class VisitDynamicObjectCreationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public class P
		{
			public int X;

			public P(int x)
			{
				X = x;
			}
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
				dynamic p = new P(5);
				yield return (int)p.X;
			}
		}
		""";
}
