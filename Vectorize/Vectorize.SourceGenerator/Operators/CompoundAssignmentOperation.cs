using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetCompoundAssignmentValue(Compilation compilation, ICompoundAssignmentOperation compoundAssignmentOperation)
	{
		var target = GetConstantValue(compilation, compoundAssignmentOperation.Target);
		var value = GetConstantValue(compilation, compoundAssignmentOperation.Value);
		var operatorKind = compoundAssignmentOperation.OperatorKind;

		var result = ExecuteBinaryOperation(operatorKind, target, value);

		variables[GetVariableName(compoundAssignmentOperation.Target)] = result;

		return null;
	}
}