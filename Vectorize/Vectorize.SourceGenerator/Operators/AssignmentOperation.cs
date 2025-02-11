using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetAssignmentValue(IAssignmentOperation assignmentOperation)
	{
		variables[GetVariableName(assignmentOperation.Target)] = GetConstantValue(assignmentOperation.Value);
		
		return null;
	}
}