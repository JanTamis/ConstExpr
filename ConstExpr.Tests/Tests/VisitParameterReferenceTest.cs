namespace ConstExpr.Tests.Tests;

public class VisitParameterReferenceTest : BaseTest<int>
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
				Run(2);
			}

			[ConstExpr]
			public static IEnumerable<int> Run(int p)
			{
				yield return p;
			}
		}
		""";
}
