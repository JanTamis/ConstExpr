namespace ConstExpr.Tests.Tests;

public class VisitFieldReferenceConstTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [10];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Holder
		{
			public const int C = 10;
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
				yield return Holder.C;
			}
		}
		""";
}
