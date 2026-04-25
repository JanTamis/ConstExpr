using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Ieee754RemainderFunctionOptimizer() : BaseMathFunctionOptimizer("Ieee754Remainder", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Math.Ieee754Remainder(x, y) → float.Ieee754Remainder(x, y)
		//                             / double.Ieee754Remainder(x, y)
		// Re-targeting to the type-specific static method removes the Math dispatch
		// overhead and allows the JIT to recognise the intrinsic directly.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

