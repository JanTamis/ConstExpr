namespace ConstExpr.Tests.Tests;

public class VisitSwitchExpressionTest : BaseTest<string>
{
	public override IEnumerable<string> Result => ["small", "even", "default"];

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
				string D(int x) => x switch
				{
					< 2 => "small",
					_ when x % 2 == 0 => "even",
					_ => "default"
				};

				return new[] { D(1), D(2), D(5) };
			}
		}
		""";
}
