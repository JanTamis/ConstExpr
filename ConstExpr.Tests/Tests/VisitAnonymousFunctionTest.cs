namespace ConstExpr.Tests.Tests;

public class VisitAnonymousFunctionTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [7];

	public override string SourceCode => """
		using System;
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
				Func<int, int> f = x => x + 2;
				yield return f(5);
			}
		}
		""";
}
