namespace ConstExpr.Tests.Tests;

public class VisitFieldReferenceNonConstTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [3];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Holder
		{
			public static int V = 3;
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
				yield return Holder.V;
			}
		}
		""";
}
