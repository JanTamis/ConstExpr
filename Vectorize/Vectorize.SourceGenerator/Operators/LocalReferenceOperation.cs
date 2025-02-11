using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetLocalValue(ILocalReferenceOperation localReferenceOperation)
	{
		var name = localReferenceOperation.Local.Name;

		if (variables.TryGetValue(name, out var value))
		{
			return value;
		}

		return null;
	}
}