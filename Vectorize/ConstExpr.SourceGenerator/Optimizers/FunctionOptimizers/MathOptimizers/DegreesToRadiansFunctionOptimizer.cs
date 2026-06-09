using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class DegreesToRadiansFunctionOptimizer() : BaseMathFunctionOptimizer("DegreesToRadians", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (context.FastMathFlags.HasFlag(FastMathFlags.AssociativeMath)
		    && TryCreateLiteral(GetConstant(paramType), out var literalValue))
		{
			result = MultiplyExpression(context.VisitedParameters[0], literalValue);
			return true;
		}

		// Delegate to the built-in T.DegreesToRadians(x) for all numeric types.
		//
		// Benchmarks (Apple M4 Pro, ARM64, .NET 10, N=1024):
		//   float.DegreesToRadians  : 0.538 ns  (baseline)
		//   x * (MathF.PI / 180f)   : 0.593 ns  (+10% slower, 1 ULP less accurate)
		//
		// The JIT recognises float/double.DegreesToRadians as a vectorisable loop and
		// emits multi-accumulator SIMD code. The explicit multiply-by-constant does not
		// trigger the same optimisation. The builtin also uses the more precise constant
		// (float)(Math.PI / 180.0) = 0x3C8EFA35 vs MathF.PI / 180f = 0x3C8EFA36 (1 ULP off).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private object GetConstant(ITypeSymbol paramType)
	{
		return paramType.SpecialType switch
		{
			SpecialType.System_Single => MathF.PI / 180f,
			SpecialType.System_Double => Math.PI / 180d,
			SpecialType.System_Int32 => Math.PI / 180d,
			SpecialType.System_Int64 => Math.PI / 180d,
			SpecialType.System_UInt32 => Math.PI / 180d,
			SpecialType.System_UInt64 => Math.PI / 180d,
			SpecialType.System_Int16 => Math.PI / 180d,
			SpecialType.System_UInt16 => Math.PI / 180d,
			_ => throw new ArgumentOutOfRangeException()
		};
	}
}