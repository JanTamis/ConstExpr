namespace ConstExpr.Tests.Tests;

public class VisitPropertyIndexerGetTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [7];

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
				var list = new List<int> { 5, 6, 7 };
				yield return list[2]; // VisitPropertyReference indexer get
			}
		}
		""";
}
