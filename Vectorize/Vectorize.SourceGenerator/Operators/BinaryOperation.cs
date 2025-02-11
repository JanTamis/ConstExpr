using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Vectorize.Helpers;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetBinaryValue(IBinaryOperation binaryOperation)
	{
		var left = GetConstantValue(binaryOperation.LeftOperand);
		var right = GetConstantValue(binaryOperation.RightOperand);
		var operatorKind = binaryOperation.OperatorKind;
		var method = binaryOperation.OperatorMethod;
		
		if (method != null)
		{
			return SyntaxHelpers.ExecuteMethod(method, null, left, right);
		}
		
		return ExecuteBinaryOperation(operatorKind, left, right);
	}
}