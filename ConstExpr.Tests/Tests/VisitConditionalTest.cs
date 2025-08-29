namespace ConstExpr.Tests.Tests;

public class VisitConditionalTest : BaseTest<int>
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
				bool f = true;
				yield return f ? 1 : 2; // VisitConditional
				f = false;
				yield return f ? 1 : 2;
			}
		}
		""";
}
