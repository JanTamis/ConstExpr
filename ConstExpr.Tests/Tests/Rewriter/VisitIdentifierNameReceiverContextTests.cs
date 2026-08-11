using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Ensures identifier receivers are not inlined into invalid collection expressions.
/// </summary>
[InheritsTests]
public class VisitIdentifierNameReceiverContextTests : BaseTestWithRandomValues<Func<double[], int[]>>
{

	public override string TestMethod => GetString(data =>
	{
		var outliers = new List<int>();

		for (var i = 0; i < data.Length; i++)
		{
			if (data[i] > 0)
			{
				outliers.Add(i);
			}
		}

		return outliers.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(data =>
		{
			ref var dataRef = ref MemoryMarshal.GetArrayDataReference(data);
			var outliers = new List<int>();

			for (var i = 0; i < data.Length; i++)
			{
				if (Unsafe.Add(ref dataRef, i) > 0D)
					outliers.Add(i);
			}

			return outliers.ToArray();
		}, [ Unknown ])
	];
}