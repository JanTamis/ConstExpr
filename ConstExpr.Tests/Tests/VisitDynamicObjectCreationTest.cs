namespace ConstExpr.Tests.Tests;

public class VisitDynamicObjectCreationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [1];

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
				dynamic p = new List<int> { 5 };
				yield return (int)p.Count;
			}
		}
		""";
}
