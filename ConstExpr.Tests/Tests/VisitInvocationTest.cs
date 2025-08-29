namespace ConstExpr.Tests.Tests;

public class VisitInvocationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [42];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Helpers
		{
			public static int Id(int x) => x;
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
				yield return Helpers.Id(42); // VisitInvocation
			}
		}
		""";
}
