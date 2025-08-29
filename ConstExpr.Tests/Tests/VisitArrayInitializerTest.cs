namespace ConstExpr.Tests.Tests;

public class VisitArrayInitializerTest : BaseTest<int>
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
				var arr = new int[] { 1, 2 }; // VisitArrayInitializer
				yield return arr[1]; // ensure value
			}
		}
		""";
}
