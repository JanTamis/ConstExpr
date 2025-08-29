namespace ConstExpr.Tests.Tests;

public class VisitAnonymousObjectCreationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5];

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
				var a = new { Y = 5 }; // VisitAnonymousObjectCreation
				yield return a.Y;
			}
		}
		""";
}
