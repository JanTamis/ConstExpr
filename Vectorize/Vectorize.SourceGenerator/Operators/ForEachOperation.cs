using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetForEachValue(IForEachLoopOperation forEachLoopOperation)
	{
		var collection = GetConstantValue(forEachLoopOperation.Collection);
		
		foreach (var element in (System.Collections.IEnumerable)collection)
		{
			variables[GetVariableName(forEachLoopOperation.LoopControlVariable)] = element;

			GetConstantValue(forEachLoopOperation.Body);
		}
		
		return null;
	}
}