namespace ConstExpr.Tests.Tests;

public class VisitInterpolatedStringTextTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["Hello"];

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
				yield return $"Hello";
			}
		}
		""";
}
