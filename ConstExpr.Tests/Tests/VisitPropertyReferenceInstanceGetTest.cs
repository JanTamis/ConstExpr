namespace ConstExpr.Tests.Tests;

public class VisitPropertyReferenceInstanceGetTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;
		namespace Testing;

		public class P
		{
			public int X { get; set; } = 5;
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
				var p = new P();
				yield return p.X;
			}
		}
		""";
}
