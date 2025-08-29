namespace ConstExpr.Tests.Tests;

public class VisitSimpleAssignmentVariableTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [4];

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
				int x;
				x = 4;
				yield return x;
			}
		}
		""";
}
