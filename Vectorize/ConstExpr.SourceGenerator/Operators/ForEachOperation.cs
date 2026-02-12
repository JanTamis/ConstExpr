using System.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetForEachValue(Compilation compilation, IForEachLoopOperation forEachLoopOperation)
	{
		var collection = GetConstantValue(compilation, forEachLoopOperation.Collection);
		var variableName = GetVariableName(forEachLoopOperation.LoopControlVariable);

		foreach (var element in (IEnumerable)collection)
		{
			variables[variableName] = element;

			GetConstantValue(compilation, forEachLoopOperation.Body);
		}

		return null;
	}
}