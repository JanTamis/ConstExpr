namespace ConstExpr.Tests.Tests;

public class VisitForLoopTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [3];

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
				int sum = 0;

				for (int i = 1; i <= 2; i++) // VisitForLoop
				{
					sum += i; // VisitCompoundAssignment too, but focus is for
				}

				yield return sum; // 3
			}
		}
		""";
}
