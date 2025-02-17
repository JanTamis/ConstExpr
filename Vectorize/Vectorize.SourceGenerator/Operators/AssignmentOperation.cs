using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetAssignmentValue(Compilation compilation, IAssignmentOperation assignmentOperation)
	{
		variables[GetVariableName(assignmentOperation.Target)] = GetConstantValue(compilation, assignmentOperation.Value);

		return null;
	}
}