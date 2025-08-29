namespace ConstExpr.Tests.Tests;

public class VisitIsNullTest : BaseTest<bool>
{
	public override IEnumerable<bool> Result => [true];

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
				object? o = null;
				yield return o is null;
			}
		}
		""";
}
