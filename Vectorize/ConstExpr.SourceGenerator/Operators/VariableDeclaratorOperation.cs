using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetVariableDeclaratorValue(Compilation compilation, IVariableDeclaratorOperation assignmentOperation)
	{
		var name = assignmentOperation.Symbol.Name;
		var value = GetConstantValue(compilation, assignmentOperation.Initializer.Value);

		variables.Add(name, value);

		return value;
	}
}