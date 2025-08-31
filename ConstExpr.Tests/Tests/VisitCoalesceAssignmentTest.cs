namespace ConstExpr.Tests.Tests;

public class VisitCoalesceAssignmentTest : BaseTest<int>
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
				int? x = null;
				x ??= 5; // VisitCoalesceAssignment
				yield return x.Value;
			}
		}
		""";
}
