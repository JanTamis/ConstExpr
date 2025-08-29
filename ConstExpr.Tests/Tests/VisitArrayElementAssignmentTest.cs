namespace ConstExpr.Tests.Tests;

public class VisitArrayElementAssignmentTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [9];

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
				var a = new int[2];
				a[1] = 9;
				yield return a[1];
			}
		}
		""";
}
