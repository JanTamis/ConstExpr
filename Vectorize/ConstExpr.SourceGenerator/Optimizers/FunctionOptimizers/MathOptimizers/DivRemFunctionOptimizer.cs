using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class DivRemFunctionOptimizer() : BaseMathFunctionOptimizer("DivRem", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Math.DivRem(a, b) → (a / b, a % b)
		// Only inline when both arguments are pure (no side-effects) to avoid
		// evaluating them twice.
		if (!IsPure(left) || !IsPure(right))
		{
			result = null;
			return false;
		}

		// Emit a tuple literal that the runtime directly destructures.
		// The JIT can further fuse the div and mod into a single IDIV instruction.
		result = TupleExpression(
			SeparatedList([
				Argument(DivideExpression(left, right)),
				Argument(ModuloExpression(left, right))
			]));

		return true;
	}
}

