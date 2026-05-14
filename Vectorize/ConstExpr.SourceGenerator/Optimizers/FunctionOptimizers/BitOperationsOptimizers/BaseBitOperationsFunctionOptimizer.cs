using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.BitOperationsOptimizers;

/// <summary>
///   Base class for <c>System.Numerics.BitOperations</c> method optimizers.
///   Validates that the method being optimized belongs to <c>BitOperations</c> before
///   delegating to the concrete optimizer.
/// </summary>
public abstract class BaseBitOperationsFunctionOptimizer(string name, Func<int, bool> isValidParameterCount)
	: BaseMathFunctionOptimizer(name, isValidParameterCount)
{
	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (context.Method.ContainingType.ToString() != "System.Numerics.BitOperations")
		{
			result = null;
			return false;
		}

		return base.TryOptimize(context, out result);
	}
}