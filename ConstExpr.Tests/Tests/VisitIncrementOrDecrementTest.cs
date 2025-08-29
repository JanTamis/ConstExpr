namespace ConstExpr.Tests.Tests;

public class VisitIncrementOrDecrementTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [1, 0];

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
				int i = 0;
				++i; // VisitIncrementOrDecrement
				yield return i;
				--i; // VisitIncrementOrDecrement
				yield return i;
			}
		}
		""";
}
