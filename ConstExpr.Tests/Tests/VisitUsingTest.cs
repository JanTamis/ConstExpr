namespace ConstExpr.Tests.Tests;

public class VisitUsingTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["disposed"];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using System.IO;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<string> Run()
			{
				using (var ms = new MemoryStream())
				{
					
				}
				
				yield return "disposed";
			}
		}
		""";
}
