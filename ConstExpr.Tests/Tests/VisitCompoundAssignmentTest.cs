namespace ConstExpr.Tests.Tests;

public class VisitCompoundAssignmentTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [6];

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
				int x = 1;
				x += 5; // VisitCompoundAssignment
				yield return x;
			}
		}
		""";
}
