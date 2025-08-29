namespace ConstExpr.Tests.Tests;

public class VisitConditionalAccessTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [-1];

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
				string? s = null;
				yield return s?.Length ?? -1; // VisitConditionalAccess + VisitCoalesce
			}
		}
		""";
}
