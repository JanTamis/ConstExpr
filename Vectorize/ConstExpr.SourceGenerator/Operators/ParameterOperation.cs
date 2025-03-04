using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetParameterValue(IParameterReferenceOperation parameterReferenceOperation)
	{
		var name = parameterReferenceOperation.Parameter.Name;

		if (variables.TryGetValue(name, out var value))
		{
			return value;
		}

		return null;
	}
}