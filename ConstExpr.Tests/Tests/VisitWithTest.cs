namespace ConstExpr.Tests.Tests;

public class VisitWithTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [10];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public record R(int A)
		{
			public int A { get; init; } = A;
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
				var r = new R(1);
				var r2 = r with { A = 10 };
				yield return r2.A;
			}
		}
		""";
}
