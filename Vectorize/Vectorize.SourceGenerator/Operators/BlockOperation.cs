using System;
using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetBlockValue(IBlockOperation blockOperation)
	{
		foreach (var operation in blockOperation.Operations)
		{
			GetConstantValue(operation);
		}
		
		return null;
	}
}