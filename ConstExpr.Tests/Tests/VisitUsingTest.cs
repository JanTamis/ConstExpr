namespace ConstExpr.Tests.Tests;

public class VisitUsingTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["disposed"];

	public override string SourceCode => """
		using System;
		using System.Collections.Generic;
		using ConstExpr.Core.Attributes;

		namespace Testing;

		file sealed class Dummy : IDisposable
		{
			public static List<string> Log = new();
			public void Dispose() => Log.Add("disposed");
		}

		public static class Classes
		{
			public static void Test()
			{
				Run();
			}

			[ConstExpr]
			public static IEnumerable<string> Run()
			{
				using var d = new Dummy();
				return Dummy.Log;
			}
		}
		""";
}
