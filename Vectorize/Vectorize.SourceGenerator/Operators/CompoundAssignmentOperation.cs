using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetCompoundAssignmentValue(ICompoundAssignmentOperation compoundAssignmentOperation)
	{
		var target = GetConstantValue(compoundAssignmentOperation.Target);
		var value = GetConstantValue(compoundAssignmentOperation.Value);
		var operatorKind = compoundAssignmentOperation.OperatorKind;
		
		var result = ExecuteBinaryOperation(operatorKind, target, value);

		variables[GetVariableName(compoundAssignmentOperation.Target)] = result;
		
		return null;
	}
}