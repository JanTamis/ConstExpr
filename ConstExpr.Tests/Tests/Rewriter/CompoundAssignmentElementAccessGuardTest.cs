using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Regression test for the compound-assignment conversion in VisitAssignmentExpression.
///   <c>x = a &lt;op&gt; b</c> may only be folded into <c>x &lt;op&gt;= b</c> when <c>a</c> is the
///   same expression as the assignment target <c>x</c>. When the binary's left operand differs from
///   the target the conversion changes semantics, so it must be left untouched.
///   Here <c>result[1] = result[0] + numbers[1]</c> must NOT become <c>result[1] += numbers[1]</c>,
///   because <c>result[0]</c> and <c>result[1]</c> are different elements.
/// </summary>
[InheritsTests]
public class CompoundAssignmentElementAccessGuardTest : BaseTest<Func<int[], int[], int[]>>
{
	public override string TestMethod => GetString((result, numbers) =>
	{
		result[1] = result[0] + numbers[1];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: the binary's left operand (result[0]) differs from the target
		// (result[1]), so the statement must stay a plain assignment, not a compound one.
		Create((result, numbers) =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			Unsafe.Add(ref resultRef, 1) = resultRef + Unsafe.Add(ref numbersRef, 1);

			return result;
		})
	];
}