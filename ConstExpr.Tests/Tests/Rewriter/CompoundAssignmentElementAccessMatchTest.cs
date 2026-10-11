using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Positive control for <see cref="CompoundAssignmentElementAccessGuardTest" />: when the binary's
///   left operand IS the assignment target, the compound conversion is valid and must still happen.
/// </summary>
// Not a random-value test: the method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.
[InheritsTests]
public class CompoundAssignmentElementAccessMatchTest : BaseTest<Func<int[], int[], int[]>>
{
	public override string TestMethod => GetString((result, numbers) =>
	{
		result[1] += numbers[1];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: result[1] = result[1] + numbers[1] is equivalent to result[1] += numbers[1].
		Create((result, numbers) =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			Unsafe.Add(ref resultRef, 1) += Unsafe.Add(ref numbersRef, 1);

			return result;
		}, [ Unknown, Unknown ]),
		Create("""
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			Unsafe.Add(ref resultRef, 1) = 22;

			return result;
			""", new[] { 1, 2, 3 }, new[] { 10, 20, 30 })
	];
}