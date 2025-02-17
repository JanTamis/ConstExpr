using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetConditionalValue(Compilation compilation, IConditionalOperation conditionalOperation)
	{
		var condition = GetConstantValue(compilation, conditionalOperation.Condition);

		if (condition is true)
		{
			return GetConstantValue(compilation, conditionalOperation.WhenTrue);
		}

		if (conditionalOperation.WhenFalse is not null)
		{
			return GetConstantValue(compilation, conditionalOperation.WhenFalse);
		}

		return null;
	}
}