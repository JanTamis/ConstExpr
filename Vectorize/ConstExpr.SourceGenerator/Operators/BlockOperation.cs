using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetBlockValue(Compilation compilation, IBlockOperation blockOperation)
	{
		foreach (var operation in blockOperation.Operations)
		{
			GetConstantValue(compilation, operation);
		}

		return null;
	}
}