namespace ConstExpr.Tests.Tests;

public class VisitDynamicMemberReferenceTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [2];

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
				dynamic o = new { A = 2 };
				yield return (int)o.A;
			}
		}
		""";
}
