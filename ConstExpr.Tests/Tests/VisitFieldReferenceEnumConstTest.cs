namespace ConstExpr.Tests.Tests;

public class VisitFieldReferenceEnumConstTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [2];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		enum E
		{
			A = 1,
			B = 2
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
				yield return (int)E.B;
			}
		}
		""";
}
