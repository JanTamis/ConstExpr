namespace ConstExpr.Tests.Tests;

public class VisitObjectCreationTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["3"];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;
		namespace Testing;

		public class P
		{
			public int X;

			public P(int x)
			{
				X = x;
			}
		}

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<string> Run()
			{
				var p = new P(3); // VisitObjectCreation
				yield return p.X.ToString();
			}
		}
		""";
}
