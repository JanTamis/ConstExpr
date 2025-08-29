namespace ConstExpr.Tests.Tests;

public class VisitVariableDeclaratorTest : BaseTest<int>
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
				int x = 3; // VisitVariableDeclarator
				yield return x;
			}
		}
		""";
}
