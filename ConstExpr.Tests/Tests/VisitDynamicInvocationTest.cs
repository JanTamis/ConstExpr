namespace ConstExpr.Tests.Tests;

public class VisitDynamicInvocationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [4];

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
				Func<int, int, int> add = (a, b) => a + b;
				dynamic d = add;
				yield return (int)d(1, 3);
			}
		}
		""";
}
