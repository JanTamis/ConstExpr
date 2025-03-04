using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Operators;

public partial class OperatorHelper
{
	private object? GetLiteralValue(ILiteralOperation literalOperation)
	{
		return literalOperation.ConstantValue.Value;
	}
}