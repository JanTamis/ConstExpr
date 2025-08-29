namespace ConstExpr.Tests.Tests;

public class VisitDefaultValueTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [0];

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
				int x = default; // VisitDefaultValue
				yield return x;
			}
		}
		""";
}
