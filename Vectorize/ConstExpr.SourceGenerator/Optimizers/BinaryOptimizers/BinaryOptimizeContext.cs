using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers
{
	public sealed class BinaryOptimizeContext
	{
		public BinaryOptimizeElement Left { get; init; }
		public BinaryOptimizeElement Right { get; init; }

		public ITypeSymbol Type { get; init; }
	}

	public sealed class BinaryOptimizeElement
	{
		public ExpressionSyntax Syntax { get; init; }
		public ITypeSymbol? Type { get; init; }
		public bool HasValue { get; init; }
		public object? Value { get; init; }
	}
}