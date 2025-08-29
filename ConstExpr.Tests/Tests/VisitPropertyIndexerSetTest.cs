namespace ConstExpr.Tests.Tests;

public class VisitPropertyIndexerSetTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [11];

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
				var d = new Dictionary<string, int>();
				d["k"] = 11;
				yield return d["k"];
			}
		}
		""";
}
