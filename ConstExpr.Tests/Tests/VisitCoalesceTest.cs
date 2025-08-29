namespace ConstExpr.Tests.Tests;

public class VisitCoalesceTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5];

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
				int? x = null;
				yield return x ?? 5; // VisitCoalesce
			}
		}
		""";
}
