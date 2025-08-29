namespace ConstExpr.Tests.Tests;

public class VisitPropertyReferenceStaticGetTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [7];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class P
		{
			public static int X { get; set; } = 7;
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
				yield return P.X;
			}
		}
		""";
}
