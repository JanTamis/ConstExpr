using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.BitOperationsOptimizers;

/// <summary>
///   Inlines <c>BitOperations.RotateRight(value, offset)</c> to bitshift expressions.
///   For <c>uint</c>:  <c>(value &gt;&gt; offset) | (value &lt;&lt; (32 - offset))</c>
///   For <c>ulong</c>: <c>(value &gt;&gt; offset) | (value &lt;&lt; (64 - offset))</c>
///   Constant arguments are folded by <c>TryExecuteWithConstantArguments</c> upstream.
/// </summary>
public class RotateRightFunctionOptimizer() : BaseBitOperationsFunctionOptimizer("RotateRight", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var value = context.VisitedParameters[0];
		var offset = context.VisitedParameters[1];

		if (!IsPure(value))
		{
			result = null;
			return false;
		}

		var bitWidth = paramType.SpecialType switch
		{
			SpecialType.System_UInt32 => 32,
			SpecialType.System_UInt64 => 64,
			_ => 0
		};

		if (bitWidth == 0)
		{
			result = null;
			return false;
		}

		// (value >> offset) | (value << (bitWidth - offset))
		result = RotateLeftFunctionOptimizer.BuildRotate(value, offset, bitWidth, false);
		return true;
	}
}