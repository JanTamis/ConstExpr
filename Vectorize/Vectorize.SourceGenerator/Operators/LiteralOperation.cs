using Microsoft.CodeAnalysis.Operations;

namespace Vectorize.Operators;

public partial class OperatorHelper
{
	private object? GetLiteralValue(ILiteralOperation literalOperation)
	{
		return literalOperation.ConstantValue.Value;
	}
}