using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetConditionalValue(IConditionalOperation conditionalOperation)
	{
		var condition = GetConstantValue(conditionalOperation.Condition);
		
		if (condition is true)
		{
			return GetConstantValue(conditionalOperation.WhenTrue);
		}

		if (conditionalOperation.WhenFalse is not null)
		{
			return GetConstantValue(conditionalOperation.WhenFalse);
		}

		return null;
	}
}