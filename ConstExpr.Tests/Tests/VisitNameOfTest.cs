namespace ConstExpr.Tests.Tests;

public class VisitNameOfTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["x"];

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
			public static IEnumerable<string> Run()
			{
				int x = 0;
				yield return nameof(x);
			}
		}
		""";
}
