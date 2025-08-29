namespace ConstExpr.Tests.Tests;

public class VisitMethodReferenceTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [10];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;
		namespace Testing;

		public static class S
		{
			public static int F(int x) => x;
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
				var d = (Func<int, int>)S.F;
				yield return d(10);
			}
		}
		""";
}
