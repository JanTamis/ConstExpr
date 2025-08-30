namespace ConstExpr.Tests.Tests;

public class VisitTryCatchFinallyTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["caught", "finally"];

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
			public static IEnumerable<string> Run()
			{
				var list = new List<string>();

				try
				{
					throw new InvalidOperationException("test");
				}
				catch (InvalidOperationException)
				{
					list.Add("caught");
				}
				finally
				{
					list.Add("finally");
				}

				return list;
			}
		}
		""";
}
