namespace ConstExpr.Tests.Tests;

public class VisitLockTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [1];

	public override string SourceCode => """
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			static readonly object O = new();

			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<int> Run()
			{
				int x = 0;

				lock (O)
				{
					x = 1;
				}

				yield return x;
			}
		}
		""";
}
