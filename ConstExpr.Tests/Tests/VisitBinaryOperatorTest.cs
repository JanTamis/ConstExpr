namespace ConstExpr.Tests.Tests;

public class VisitBinaryOperatorTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [7];

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
				yield return 3 + 4; // VisitBinaryOperator
			}
		}
		""";
}
