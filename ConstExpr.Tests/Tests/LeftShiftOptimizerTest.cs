using System.Collections.Generic;

namespace ConstExpr.Tests.Tests;

public class LeftShiftOptimizerTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5, 0];

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
				yield return 5 << 0; // x << 0 => x
				yield return 0 << 3; // 0 << x => 0
			}
		}
		""";
}

