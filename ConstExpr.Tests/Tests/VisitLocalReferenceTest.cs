namespace ConstExpr.Tests.Tests;

public class VisitLocalReferenceTest : BaseTest<int>
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
				int l = 1;
				yield return l; // VisitLocalReference
			}
		}
		""";
}
