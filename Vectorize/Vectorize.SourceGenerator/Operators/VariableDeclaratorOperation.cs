using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetVariableDeclaratorValue(IVariableDeclaratorOperation assignmentOperation)
	{
		var name = assignmentOperation.Symbol.Name;
		var value = GetConstantValue(assignmentOperation.Initializer.Value);

		variables.Add(name, value);
		
		return value;
	}
}