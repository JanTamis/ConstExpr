namespace ConstExpr.Tests.Tests;

public class VisitAwaitTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [42];

	public override string SourceCode => """
		using System.Collections.Generic;
		using System.Threading.Tasks;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static async IAsyncEnumerable<int> Run()
			{
				yield return await Task.FromResult(42); // VisitAwait
			}
		}
		""";
}
