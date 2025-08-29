namespace ConstExpr.Tests.Tests;

public class VisitIsPatternRelationalTest : BaseTest<bool>
{
	public override IEnumerable<bool> Result => [true, false];

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
			public static IEnumerable<bool> Run()
			{
				int x = 3;
				yield return x is > 2;
				yield return x is < 2;
			}
		}
		""";
}
