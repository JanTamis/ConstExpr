namespace ConstExpr.Tests.Tests;

public class VisitArrayCreationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [2];

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
				var arr = new int[2]; // VisitArrayCreation
				yield return arr.Length; // ensure it's observed
			}
		}
		""";
}
