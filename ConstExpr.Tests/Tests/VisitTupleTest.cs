namespace ConstExpr.Tests.Tests;

public class VisitTupleTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [1, 2];

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
				var t = (1, 2);
				yield return t.Item1;
				yield return t.Item2;
			}
		}
		""";
}
