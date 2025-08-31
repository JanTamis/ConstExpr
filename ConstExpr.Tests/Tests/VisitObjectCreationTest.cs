namespace ConstExpr.Tests.Tests;

public class VisitObjectCreationTest : BaseTest<int>
{
	public override IEnumerable<int> Result => [3];

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
				var list = new List<int>(3); // VisitObjectCreation
				
				yield return list.Capacity;
			}
		}
		""";
}
