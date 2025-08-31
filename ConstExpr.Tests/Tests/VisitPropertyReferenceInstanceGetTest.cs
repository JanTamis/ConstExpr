namespace ConstExpr.Tests.Tests;

public class VisitPropertyReferenceInstanceGetTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [5];

	public override string SourceCode => """
		using System;
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
			var v = new Version(5, 0);
			yield return v.Major;
			}
		}
		""";
}
