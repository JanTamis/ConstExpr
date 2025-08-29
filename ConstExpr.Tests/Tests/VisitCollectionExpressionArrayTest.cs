namespace ConstExpr.Tests.Tests;

public class VisitCollectionExpressionArrayTest : BaseTest<int>
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
				int[] a = [8, 9]; // VisitCollectionExpression
				yield return a[1];
			}
		}
		""";
}
