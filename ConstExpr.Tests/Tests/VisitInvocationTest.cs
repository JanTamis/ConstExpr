namespace ConstExpr.Tests.Tests;

public class VisitInvocationTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["42"];

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
				yield return 42.ToString() // VisitInvocation
			}
		}
		""";
}
