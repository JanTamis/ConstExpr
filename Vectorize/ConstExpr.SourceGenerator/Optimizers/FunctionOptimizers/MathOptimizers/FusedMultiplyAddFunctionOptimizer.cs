using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class FusedMultiplyAddFunctionOptimizer() : BaseMathFunctionOptimizer("FusedMultiplyAdd", n => n is 3)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Math.FusedMultiplyAdd(x, y, addend) → float.FusedMultiplyAdd(x, y, addend)
		//                                      / double.FusedMultiplyAdd(x, y, addend)
		// Re-targeting to the numeric-type static method allows the JIT to emit
		// a single VFMADD instruction (x86 FMA3 / ARM FMADD) without going through
		// the Math dispatch shim.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

