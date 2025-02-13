using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetForEachValue(IForEachLoopOperation forEachLoopOperation)
	{
		var collection = GetConstantValue(forEachLoopOperation.Collection);
		var variableName = GetVariableName(forEachLoopOperation.LoopControlVariable);
		
		foreach (var element in (System.Collections.IEnumerable)collection)
		{
			variables[variableName] = element;

			GetConstantValue(forEachLoopOperation.Body);
		}
		
		return null;
	}
}