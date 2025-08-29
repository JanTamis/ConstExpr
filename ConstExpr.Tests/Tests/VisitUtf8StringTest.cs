namespace ConstExpr.Tests.Tests;

public class VisitUtf8StringTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [65];

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
				var b = "A"u8;
				yield return b[0];
			}
		}
		""";
}
