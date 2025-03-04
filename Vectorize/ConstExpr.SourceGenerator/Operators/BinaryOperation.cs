using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetBinaryValue(Compilation compilation, IBinaryOperation binaryOperation)
	{
		var left = GetConstantValue(compilation, binaryOperation.LeftOperand);
		var right = GetConstantValue(compilation, binaryOperation.RightOperand);
		var operatorKind = binaryOperation.OperatorKind;
		var method = binaryOperation.OperatorMethod;

		if (method != null)
		{
			return SyntaxHelpers.ExecuteMethod(compilation, method, null, left, right);
		}

		return ExecuteBinaryOperation(operatorKind, left, right);
	}
}