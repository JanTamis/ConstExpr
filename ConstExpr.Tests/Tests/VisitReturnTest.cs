namespace ConstExpr.Tests.Tests;

public class VisitReturnTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [1, 2];

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
				yield return 1;
				yield return 2;
			}
		}
		""";
}
