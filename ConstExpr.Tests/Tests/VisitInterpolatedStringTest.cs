namespace ConstExpr.Tests.Tests;

public class VisitInterpolatedStringTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["X=5"];

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
				int x = 5;
				yield return $"X={x}";
			}
		}
		""";
}
