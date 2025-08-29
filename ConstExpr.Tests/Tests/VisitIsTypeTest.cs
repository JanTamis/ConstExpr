namespace ConstExpr.Tests.Tests;

public class VisitIsTypeTest : BaseTest<bool>
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
				object a = 1;
				yield return a is int;
				yield return a is string;
			}
		}
		""";
}
