using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetBinaryValue(Compilation compilation, IBinaryOperation binaryOperation, Dictionary<string, object?> variables)
	{
		var left = GetConstantValue(compilation, binaryOperation.LeftOperand);
		var right = GetConstantValue(compilation, binaryOperation.RightOperand);
		var operatorKind = binaryOperation.OperatorKind;
		var method = binaryOperation.OperatorMethod;

		if (method != null && loader.TryExecuteMethod(method, null, variables, [left, right], out var value))
		{
			return value;
		}

		return ExecuteBinaryOperation(operatorKind, left, right);
	}
}