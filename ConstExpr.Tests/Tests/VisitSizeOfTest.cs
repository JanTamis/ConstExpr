namespace ConstExpr.Tests.Tests;

public class VisitSizeOfTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [sizeof(int)];

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
				yield return sizeof(int);
			}
		}
		""";
}
