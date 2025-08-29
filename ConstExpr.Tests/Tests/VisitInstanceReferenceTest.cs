namespace ConstExpr.Tests.Tests;

public class VisitInstanceReferenceTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [42];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public class C
		{
			public int X = 42;

			[ConstExpr]
			public IEnumerable<int> M()
			{
				var self = this;
				yield return self.X;
			}
		}

		public static class Classes
		{
			public static void Test()
			{
				new C().M();
			}
		}
		""";
}
