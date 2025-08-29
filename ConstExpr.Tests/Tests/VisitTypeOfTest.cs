namespace ConstExpr.Tests.Tests;

public class VisitTypeOfTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["System.Int32"];

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
			public static IEnumerable<string> Run()
			{
				yield return typeof(int).ToString();
			}
		}
		""";
}
